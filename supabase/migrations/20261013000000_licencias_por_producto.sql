-- Licencias por producto en la base compartida de ScaleLogger (proyecto Supabase uxzehuncusnaofmfjydk).
--
-- Antes: license_keys y trials no distinguian producto, asi que una clave de ScaleLogger habria activado
-- cualquier otra aplicacion que usara la misma base. Ahora cada fila lleva `product`:
--   'scalelogger'  -> ScaleLogger (valor por defecto: las filas existentes quedan asi)
--   'pesajefaeneo' -> Sistema de Pesaje para Faeneo (Pesalab)
--
-- Compatibilidad: ScaleLogger 0.1.0 sigue llamando a activate_license / register_trial / check_license con
-- dos argumentos. Esas funciones se reescriben como envoltorios de las nuevas con product = 'scalelogger',
-- con la misma firma y la misma respuesta: no hay que recompilar ScaleLogger.
-- Las aplicaciones nuevas usan activate_product_license / register_product_trial / check_product_license.
--
-- Todo va en una transaccion: si algo falla, la base queda como estaba.
-- Aplicar en el SQL Editor de Supabase (rol postgres). Luego ejecutar las verificaciones del final.

begin;

-- 1. Columna de producto ---------------------------------------------------------------------------------

alter table public.license_keys add column if not exists product text not null default 'scalelogger';
alter table public.trials       add column if not exists product text not null default 'scalelogger';

alter table public.license_keys drop constraint if exists license_keys_product_check;
alter table public.license_keys add constraint license_keys_product_check check (product ~ '^[a-z0-9_-]{2,32}$');
alter table public.trials drop constraint if exists trials_product_check;
alter table public.trials add constraint trials_product_check check (product ~ '^[a-z0-9_-]{2,32}$');

-- La prueba es una por (producto, equipo): antes la clave primaria era solo hardware_id.
alter table public.trials drop constraint if exists trials_pkey;
alter table public.trials add constraint trials_pkey primary key (product, hardware_id);

create index if not exists license_keys_product_hwid_idx on public.license_keys (product, hardware_id);

-- 2. Funciones por producto (las que usa la app) ---------------------------------------------------------

create or replace function public._license_product_ok(p_product text)
returns boolean
language sql
immutable
set search_path = public
as $$
  select coalesce(p_product, '') ~ '^[a-z0-9_-]{2,32}$';
$$;

-- Activa una clave del producto y la liga al equipo. Atomico: solo una llamada puede consumirla.
-- Una clave de otro producto responde 'invalid' (no revela que existe).
create or replace function public.activate_product_license(p_key text, p_hwid text, p_product text)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
  v_hash text := public._license_hash(p_key);
  v_row  public.license_keys%rowtype;
begin
  if p_hwid is null or length(p_hwid) < 16 or not public._license_product_ok(p_product) then
    return jsonb_build_object('result', 'invalid');
  end if;

  update public.license_keys
     set status = 'used', hardware_id = p_hwid, activated_at = now()
   where key_hash = v_hash and product = p_product and status = 'available'
   returning * into v_row;

  if found then
    return jsonb_build_object('result', 'ok');
  end if;

  select * into v_row from public.license_keys where key_hash = v_hash and product = p_product;
  if not found then
    return jsonb_build_object('result', 'invalid');
  end if;
  if v_row.status = 'revoked' then
    return jsonb_build_object('result', 'revoked');
  end if;
  -- Ya usada: es idempotente si es el mismo equipo (reinstalacion).
  if v_row.hardware_id = p_hwid then
    return jsonb_build_object('result', 'ok');
  end if;
  return jsonb_build_object('result', 'already_used');
end;
$$;

-- Registra (una sola vez) el inicio de la prueba de este equipo para el producto y devuelve la fecha.
create or replace function public.register_product_trial(p_hwid text, p_product text)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
  v_started timestamptz;
begin
  if p_hwid is null or length(p_hwid) < 16 or not public._license_product_ok(p_product) then
    return jsonb_build_object('result', 'invalid');
  end if;

  insert into public.trials (product, hardware_id) values (p_product, p_hwid)
  on conflict (product, hardware_id) do nothing;

  select started_at into v_started from public.trials where product = p_product and hardware_id = p_hwid;
  return jsonb_build_object('result', 'ok', 'started_at', extract(epoch from v_started)::bigint);
end;
$$;

-- Estado de este equipo para el producto (detecta revocaciones y restaura activacion y prueba).
create or replace function public.check_product_license(p_hwid text, p_product text)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
  v_status  text;
  v_started timestamptz;
begin
  if p_hwid is null or length(p_hwid) < 16 or not public._license_product_ok(p_product) then
    return jsonb_build_object('status', 'none');
  end if;

  select status into v_status
    from public.license_keys
   where product = p_product and hardware_id = p_hwid
   order by activated_at desc nulls last
   limit 1;

  select started_at into v_started from public.trials where product = p_product and hardware_id = p_hwid;

  return jsonb_build_object(
    'status', case when v_status = 'used' then 'activated'
                   when v_status = 'revoked' then 'revoked'
                   else 'none' end,
    'trial_started_at', case when v_started is null then null
                             else extract(epoch from v_started)::bigint end
  );
end;
$$;

-- 3. Funciones de ScaleLogger 0.1.0: misma firma y respuesta, ahora limitadas a 'scalelogger' ------------

create or replace function public.activate_license(p_key text, p_hwid text)
returns jsonb
language sql
security definer
set search_path = public
as $$
  select public.activate_product_license(p_key, p_hwid, 'scalelogger');
$$;

create or replace function public.register_trial(p_hwid text)
returns jsonb
language sql
security definer
set search_path = public
as $$
  select public.register_product_trial(p_hwid, 'scalelogger');
$$;

create or replace function public.check_license(p_hwid text)
returns jsonb
language sql
security definer
set search_path = public
as $$
  select public.check_product_license(p_hwid, 'scalelogger');
$$;

-- 4. Administracion (solo desde el SQL Editor) ------------------------------------------------------------

-- Genera claves para un producto. Devuelve las claves en claro UNA vez; en la base solo queda el hash.
create or replace function public.generate_product_license_keys(p_product text, p_count int, p_note text default null)
returns table (license_key text)
language plpgsql
security definer
set search_path = public
as $$
declare
  v_raw text;
  v_key text;
  i int;
begin
  if not public._license_product_ok(p_product) then
    raise exception 'Producto invalido: %', p_product;
  end if;
  for i in 1..greatest(p_count, 1) loop
    v_raw := upper(substr(replace(gen_random_uuid()::text || gen_random_uuid()::text, '-', ''), 1, 20));
    v_key := substr(v_raw, 1, 4) || '-' || substr(v_raw, 5, 4) || '-' || substr(v_raw, 9, 4)
             || '-' || substr(v_raw, 13, 4) || '-' || substr(v_raw, 17, 4);
    insert into public.license_keys (key_hash, note, product) values (public._license_hash(v_key), p_note, p_product);
    license_key := v_key;
    return next;
  end loop;
end;
$$;

-- El generador original sigue creando claves de ScaleLogger.
create or replace function public.generate_license_keys(p_count int, p_note text default null)
returns table (license_key text)
language sql
security definer
set search_path = public
as $$
  select * from public.generate_product_license_keys('scalelogger', p_count, p_note);
$$;

-- Revoca una clave (el equipo pasa a modo consulta en su proximo arranque con internet).
create or replace function public.revoke_license_key(p_key text)
returns text
language plpgsql
security definer
set search_path = public
as $$
begin
  update public.license_keys set status = 'revoked' where key_hash = public._license_hash(p_key);
  return case when found then 'revocada' else 'no existe' end;
end;
$$;

-- Libera una clave usada para activarla en otro equipo (cambio de PC). El equipo anterior conserva
-- su activacion local hasta que se revoque otra clave suya o se borre su registro.
create or replace function public.release_license_key(p_key text)
returns text
language plpgsql
security definer
set search_path = public
as $$
begin
  update public.license_keys
     set status = 'available', hardware_id = null, activated_at = null
   where key_hash = public._license_hash(p_key) and status = 'used';
  return case when found then 'liberada' else 'no existe o no estaba usada' end;
end;
$$;

-- 5. Permisos ---------------------------------------------------------------------------------------------

revoke all on function public._license_product_ok(text) from public, anon, authenticated;
revoke all on function public.activate_product_license(text, text, text) from public, anon, authenticated;
revoke all on function public.register_product_trial(text, text) from public, anon, authenticated;
revoke all on function public.check_product_license(text, text) from public, anon, authenticated;
revoke all on function public.activate_license(text, text) from public, anon, authenticated;
revoke all on function public.register_trial(text) from public, anon, authenticated;
revoke all on function public.check_license(text) from public, anon, authenticated;
revoke all on function public.generate_product_license_keys(text, int, text) from public, anon, authenticated;
revoke all on function public.generate_license_keys(int, text) from public, anon, authenticated;
revoke all on function public.revoke_license_key(text) from public, anon, authenticated;
revoke all on function public.release_license_key(text) from public, anon, authenticated;

grant execute on function public.activate_product_license(text, text, text) to anon;
grant execute on function public.register_product_trial(text, text) to anon;
grant execute on function public.check_product_license(text, text) to anon;
grant execute on function public.activate_license(text, text) to anon;
grant execute on function public.register_trial(text) to anon;
grant execute on function public.check_license(text) to anon;

commit;

-- PostgREST: recargar el esquema para que vea las funciones nuevas.
notify pgrst, 'reload schema';

-- =========================================================================================================
-- Verificaciones (ejecutar despues; no modifican nada salvo las filas de prueba, que se borran al final)
-- =========================================================================================================
--
-- a) Las filas existentes quedaron como ScaleLogger:
--    select product, status, count(*) from public.license_keys group by 1, 2;
--    select product, count(*) from public.trials group by 1;
--
-- b) Permisos de anon (deben ser true solo las 6 funciones de la app):
--    select p.proname, has_function_privilege('anon', p.oid, 'execute') as anon
--      from pg_proc p join pg_namespace n on n.oid = p.pronamespace
--     where n.nspname = 'public' and p.proname like any (array['%license%', '%trial%'])
--     order by 1;
--
-- c) Aislamiento entre productos (hwid ficticio):
--    select * from public.generate_product_license_keys('pesajefaeneo', 1, 'prueba-aislamiento');  -- anotar la clave
--    select public.activate_license('<clave>', 'test-hwid-0000000000000001');                       -- debe dar invalid
--    select public.activate_product_license('<clave>', 'test-hwid-0000000000000001', 'pesajefaeneo'); -- ok
--    select public.check_product_license('test-hwid-0000000000000001', 'pesajefaeneo');             -- activated
--    select public.check_license('test-hwid-0000000000000001');                                     -- none
--    select public.register_product_trial('test-hwid-0000000000000001', 'pesajefaeneo');
--    select public.register_trial('test-hwid-0000000000000001');                                    -- otra prueba, otra fecha
--    -- limpieza:
--    delete from public.license_keys where note = 'prueba-aislamiento';
--    delete from public.trials where hardware_id = 'test-hwid-0000000000000001';
--
-- =========================================================================================================
-- Vuelta atras (solo si hiciera falta y SIN filas de 'pesajefaeneo'; si las hay, borrarlas antes)
-- =========================================================================================================
--    begin;
--    drop function public.release_license_key(text);
--    drop function public.revoke_license_key(text);
--    drop function public.generate_product_license_keys(text, int, text);
--    drop function public.activate_product_license(text, text, text) cascade;
--    drop function public.register_product_trial(text, text) cascade;
--    drop function public.check_product_license(text, text) cascade;
--    drop function public._license_product_ok(text);
--    alter table public.trials drop constraint trials_pkey;
--    alter table public.trials add constraint trials_pkey primary key (hardware_id);
--    alter table public.trials drop column product;
--    alter table public.license_keys drop column product;
--    commit;
--    -- Despues, volver a ejecutar supabase/migrations/20261008000000_licensing.sql del repositorio de
--    -- ScaleLogger: recrea activate_license / register_trial / check_license / generate_license_keys originales.
