"""Analizador del dialecto Markdown de los manuales (ver fuente/FORMATO.md).

Convierte un archivo .md en un arbol (diccionarios y listas) que consumen por igual el
generador de HTML y el generador de Word, de modo que ambos salgan del MISMO contenido.
"""
from __future__ import annotations

import datetime
import re
import unicodedata

MESES = ["enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto",
         "septiembre", "octubre", "noviembre", "diciembre"]

TIPOS_RECUADRO = {
    "IMPORTANTE": ("important", "Importante", "!"),
    "SI-ALGO-SALE-MAL": ("error", "Si algo sale mal", "?"),
    "CONSEJO": ("tip", "Consejo", "i"),
}

RE_HEADING = re.compile(r"^(#{1,3})\s+(.*?)\s*(?:\{(\w[\w-]*)\})?\s*$")
RE_IMAGEN = re.compile(r"^!\[(.*)\]\(([^)]+)\)\s*(?:\{([^}]*)\})?\s*$")
RE_ORDENADA = re.compile(r"^(\s*)(\d+)[.)]\s+(.*)$")
RE_VINETA = re.compile(r"^(\s*)[-*]\s+(.*)$")
RE_SEPARADOR_TABLA = re.compile(r"^\s*\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?\s*$")
RE_SALTO = re.compile(r"^\s*<!--\s*salto\s*-->\s*$", re.I)
RE_COMENTARIO = re.compile(r"^\s*<!--.*-->\s*$")


class ErrorFormato(Exception):
    pass


# ----------------------------------------------------------------------------- inline
def parse_inline(texto: str) -> list[dict]:
    """Devuelve una lista de tramos: {t:'txt', v, b, i} o {t:'key', v}."""
    tramos: list[dict] = []
    buf: list[str] = []
    negrita = False
    cursiva = False

    def vaciar():
        if buf:
            tramos.append({"t": "txt", "v": "".join(buf), "b": negrita, "i": cursiva})
            buf.clear()

    i = 0
    n = len(texto)
    while i < n:
        c = texto[i]
        if c == "\\" and i + 1 < n and texto[i + 1] in "*`\\[]()#<>|_!":
            buf.append(texto[i + 1])
            i += 2
        elif c == "`":
            j = texto.find("`", i + 1)
            if j == -1:
                buf.append(c)
                i += 1
            else:
                vaciar()
                contenido = texto[i + 1:j]
                partes = [p for p in contenido.split("+")] if "+" in contenido and contenido != "+" else [contenido]
                if any(p.strip() == "" for p in partes):
                    partes = [contenido]
                for k, p in enumerate(partes):
                    if k:
                        tramos.append({"t": "txt", "v": "+", "b": negrita, "i": cursiva})
                    tramos.append({"t": "key", "v": p.strip(), "b": negrita, "i": cursiva})
                i = j + 1
        elif texto.startswith("**", i):
            vaciar()
            negrita = not negrita
            i += 2
        elif c == "*":
            vaciar()
            cursiva = not cursiva
            i += 1
        else:
            buf.append(c)
            i += 1
    vaciar()
    return tramos


def texto_plano(tramos: list[dict]) -> str:
    return "".join(t["v"] for t in tramos)


def _sin_acentos(s: str) -> str:
    return "".join(ch for ch in unicodedata.normalize("NFD", s) if unicodedata.category(ch) != "Mn")


# ----------------------------------------------------------------------------- bloques
def _es_inicio_de_bloque(linea: str, siguiente: str | None) -> bool:
    s = linea.strip()
    if not s:
        return True
    if RE_HEADING.match(s) or RE_IMAGEN.match(s) or RE_SALTO.match(s) or RE_COMENTARIO.match(s):
        return True
    if s.startswith(">"):
        return True
    if RE_ORDENADA.match(linea) or RE_VINETA.match(linea):
        return True
    if s.startswith("|") and siguiente is not None and RE_SEPARADOR_TABLA.match(siguiente):
        return True
    return False


def _celdas(linea: str) -> list[str]:
    s = linea.strip()
    if s.startswith("|"):
        s = s[1:]
    if s.endswith("|") and not s.endswith("\\|"):
        s = s[:-1]
    celdas, actual, i = [], [], 0
    while i < len(s):
        if s[i] == "\\" and i + 1 < len(s) and s[i + 1] == "|":
            actual.append("|")
            i += 2
        elif s[i] == "|":
            celdas.append("".join(actual).strip())
            actual = []
            i += 1
        else:
            actual.append(s[i])
            i += 1
    celdas.append("".join(actual).strip())
    return celdas


def _parse_atributos(attr: str | None, ctx: str) -> dict:
    res: dict = {}
    if not attr:
        return res
    for par in re.split(r"(?<![,+:])[;\s]+(?=\w+=)", attr.strip()):
        par = par.strip().strip(";")
        if not par:
            continue
        if "=" not in par:
            raise ErrorFormato(f"{ctx}: atributo mal formado '{par}'")
        k, v = par.split("=", 1)
        k = k.strip().lower()
        if k not in ("callouts", "ancho", "recorte", "margen"):
            raise ErrorFormato(f"{ctx}: atributo desconocido '{k}' (use callouts, ancho, recorte, margen)")
        res[k] = v.strip().rstrip(",")
    return res


def _parse_recorte(attrs: dict, ctx: str) -> dict | None:
    """{recorte=x,y,ancho,alto} (pixeles de la captura) o {recorte=control:a+b[,margen=N]}."""
    valor = attrs.get("recorte")
    if valor is None:
        if "margen" in attrs:
            raise ErrorFormato(f"{ctx}: 'margen' solo tiene sentido junto con recorte=control:...")
        return None
    margen = None
    m = re.search(r",\s*margen\s*=\s*(\d+)\s*$", valor)
    if m:
        margen = int(m.group(1))
        valor = valor[:m.start()]
    if "margen" in attrs:
        margen = int(attrs["margen"]) if attrs["margen"].isdigit() else None
        if margen is None:
            raise ErrorFormato(f"{ctx}: margen invalido '{attrs['margen']}' (entero en pixeles)")
    if valor.lower().startswith("control:"):
        nombres = [n.strip() for n in valor[8:].split("+") if n.strip()]
        if not nombres:
            raise ErrorFormato(f"{ctx}: recorte=control: necesita al menos un nombre (a+b)")
        return {"modo": "control", "controles": nombres, "margen": 12 if margen is None else margen}
    nums = re.findall(r"-?\d+", valor)
    if len(nums) != 4 or not re.fullmatch(r"\s*-?\d+\s*(,\s*-?\d+\s*){3}", valor):
        raise ErrorFormato(f"{ctx}: recorte debe ser x,y,ancho,alto (pixeles) o control:nombre[+nombre]")
    if margen is not None:
        raise ErrorFormato(f"{ctx}: 'margen' solo se usa con recorte=control:...")
    x, y, w, h = (int(n) for n in nums)
    if w <= 0 or h <= 0:
        raise ErrorFormato(f"{ctx}: recorte con ancho o alto no positivo")
    return {"modo": "rect", "rect": [x, y, w, h]}


class _Analizador:
    def __init__(self, nombre: str):
        self.nombre = nombre

    def bloques(self, lineas: list[str], base_linea: int = 1) -> list[dict]:
        out: list[dict] = []
        i = 0
        n = len(lineas)
        while i < n:
            linea = lineas[i]
            s = linea.strip()
            ctx = f"{self.nombre}:{base_linea + i}"
            if not s:
                i += 1
                continue
            if RE_SALTO.match(s):
                out.append({"tipo": "salto"})
                i += 1
                continue
            if RE_COMENTARIO.match(s):
                i += 1
                continue
            m = RE_HEADING.match(s)
            if m:
                out.append({"tipo": "titulo", "nivel": len(m.group(1)),
                            "inl": parse_inline(m.group(2)), "especial": (m.group(3) or "").lower()})
                i += 1
                continue
            m = RE_IMAGEN.match(s)
            if m:
                attrs = _parse_atributos(m.group(3), ctx)
                ruta = m.group(2).strip()
                idimg = re.sub(r"\.(png|jpg|jpeg)$", "", ruta.replace("\\", "/").split("/")[-1], flags=re.I)
                callouts = [int(x) for x in re.findall(r"\d+", attrs.get("callouts", ""))]
                ancho = None
                if "ancho" in attrs:
                    try:
                        ancho = float(attrs["ancho"].replace(",", "."))
                    except ValueError:
                        raise ErrorFormato(f"{ctx}: ancho invalido '{attrs['ancho']}'")
                out.append({"tipo": "imagen", "id": idimg, "epigrafe": parse_inline(m.group(1)),
                            "callouts": callouts, "ancho_cm": ancho, "linea": base_linea + i,
                            "recorte": _parse_recorte(attrs, ctx)})
                i += 1
                continue
            if s.startswith(">"):
                j = i
                cuerpo = []
                while j < n and lineas[j].strip().startswith(">"):
                    cuerpo.append(re.sub(r"^\s*>\s?", "", lineas[j]))
                    j += 1
                m = re.match(r"^\[!([A-Za-z-]+)\]\s*$", cuerpo[0].strip())
                if not m:
                    raise ErrorFormato(f"{ctx}: bloque '>' debe empezar con [!IMPORTANTE], [!SI-ALGO-SALE-MAL] o [!CONSEJO]")
                tipo = m.group(1).upper()
                if tipo not in TIPOS_RECUADRO:
                    raise ErrorFormato(f"{ctx}: tipo de recuadro desconocido '{tipo}'")
                clave, etiqueta, icono = TIPOS_RECUADRO[tipo]
                out.append({"tipo": "recuadro", "clase": clave, "etiqueta": etiqueta, "icono": icono,
                            "bloques": self.bloques(cuerpo[1:], base_linea + i + 1)})
                i = j
                continue
            if s.startswith("|") and i + 1 < n and RE_SEPARADOR_TABLA.match(lineas[i + 1]):
                cab = _celdas(lineas[i])
                seps = _celdas(lineas[i + 1])
                alin = []
                for sp in seps:
                    if sp.startswith(":") and sp.endswith(":"):
                        alin.append("center")
                    elif sp.endswith(":"):
                        alin.append("right")
                    else:
                        alin.append("left")
                j = i + 2
                filas = []
                while j < n and lineas[j].strip().startswith("|"):
                    fila = _celdas(lineas[j])
                    fila = (fila + [""] * len(cab))[:len(cab)]
                    filas.append([parse_inline(c) for c in fila])
                    j += 1
                out.append({"tipo": "tabla", "cabecera": [parse_inline(c) for c in cab],
                            "filas": filas, "alineacion": (alin + ["left"] * len(cab))[:len(cab)]})
                i = j
                continue
            mo = RE_ORDENADA.match(linea)
            mv = RE_VINETA.match(linea)
            if mo or mv:
                ordenada = bool(mo)
                items = []
                j = i
                while j < n:
                    ml = (RE_ORDENADA if ordenada else RE_VINETA).match(lineas[j])
                    if not ml or len(ml.group(1)) > 1:
                        break
                    primera = ml.group(3) if ordenada else ml.group(2)
                    ancho_marca = 3
                    cuerpo = [primera]
                    k = j + 1
                    while k < n:
                        lk = lineas[k]
                        if not lk.strip():
                            # una linea en blanco pertenece al item solo si lo que sigue esta sangrado
                            kk = k
                            while kk < n and not lineas[kk].strip():
                                kk += 1
                            if kk < n and re.match(r"^ {2,}\S", lineas[kk]):
                                cuerpo.extend([""] * (kk - k))
                                k = kk
                                continue
                            break
                        if re.match(r"^ {2,}\S", lk):
                            cuerpo.append(lk[min(ancho_marca, len(lk) - len(lk.lstrip(' '))):])
                            k += 1
                            continue
                        break
                    items.append({"bloques": self.bloques(cuerpo, base_linea + j)})
                    j = k
                    # saltar lineas en blanco entre items de la misma lista
                    kk = j
                    while kk < n and not lineas[kk].strip():
                        kk += 1
                    if kk < n and (RE_ORDENADA if ordenada else RE_VINETA).match(lineas[kk]) \
                            and len((RE_ORDENADA if ordenada else RE_VINETA).match(lineas[kk]).group(1)) <= 1:
                        j = kk
                    else:
                        break
                out.append({"tipo": "lista", "ordenada": ordenada, "items": items})
                i = j
                continue
            # parrafo
            j = i
            partes = []
            while j < n and not (j > i and _es_inicio_de_bloque(lineas[j], lineas[j + 1] if j + 1 < n else None)):
                partes.append(lineas[j].strip())
                j += 1
            out.append({"tipo": "parrafo", "inl": parse_inline(" ".join(partes))})
            i = j
        return out


# ----------------------------------------------------------------------------- documento
def fecha_en_espanol(d: datetime.date) -> str:
    return f"{d.day} de {MESES[d.month - 1]} de {d.year}"


def _front_matter(texto: str, nombre: str) -> tuple[dict, list[str], int]:
    lineas = texto.splitlines()
    meta: dict = {}
    if not lineas or lineas[0].strip() != "---":
        raise ErrorFormato(f"{nombre}: falta el bloque inicial '---' con titulo, rol, version y fecha")
    k = 1
    while k < len(lineas) and lineas[k].strip() != "---":
        if lineas[k].strip():
            if ":" not in lineas[k]:
                raise ErrorFormato(f"{nombre}:{k + 1}: linea de cabecera sin ':'")
            c, v = lineas[k].split(":", 1)
            meta[c.strip().lower()] = v.strip().strip('"')
        k += 1
    if k >= len(lineas):
        raise ErrorFormato(f"{nombre}: el bloque inicial no se cierra con '---'")
    for req in ("titulo", "rol", "version"):
        if req not in meta:
            raise ErrorFormato(f"{nombre}: falta '{req}' en la cabecera")
    return meta, lineas[k + 1:], k + 2


def _numerar(bloques: list[dict]) -> list[dict]:
    """Numera los titulos (capitulos y secciones) y calcula la lista del indice."""
    cap = sec = 0
    en_especial = False
    toc: list[dict] = []
    id_n = 0
    for b in bloques:
        if b["tipo"] != "titulo":
            continue
        id_n += 1
        b["id"] = f"t{id_n}"
        txt = texto_plano(b["inl"])
        if b["nivel"] == 1:
            en_especial = b["especial"] in ("glosario", "teclas")
            if en_especial:
                b["numero"] = ""
            else:
                cap += 1
                sec = 0
                b["numero"] = f"{cap}"
        elif b["nivel"] == 2:
            if en_especial or b["especial"] or not cap:
                b["numero"] = ""
            else:
                sec += 1
                b["numero"] = f"{cap}.{sec}"
        else:
            b["numero"] = ""
        if b["nivel"] == 1 or (b["nivel"] == 2 and not en_especial):
            toc.append({"id": b["id"], "nivel": b["nivel"], "numero": b["numero"], "texto": txt})
    return toc


def _glosario(bloques: list[dict]) -> None:
    """Dentro de un capitulo {glosario}, las listas se vuelven filas termino/definicion ordenadas."""
    en_glosario = False
    for idx, b in enumerate(bloques):
        if b["tipo"] == "titulo" and b["nivel"] == 1:
            en_glosario = b["especial"] == "glosario"
        elif en_glosario and b["tipo"] == "lista":
            filas = []
            for it in b["items"]:
                p = next((x for x in it["bloques"] if x["tipo"] == "parrafo"), None)
                if not p:
                    raise ErrorFormato("Glosario: cada elemento debe ser '- **Termino**: definicion'")
                inl = p["inl"]
                k = 0
                while k < len(inl) and inl[k].get("b") and inl[k]["t"] == "txt":
                    k += 1
                if k == 0:
                    raise ErrorFormato("Glosario: cada elemento debe empezar con el termino en **negrita**")
                termino = texto_plano(inl[:k]).strip()
                resto = inl[k:]
                if resto and resto[0]["t"] == "txt":
                    resto[0] = dict(resto[0], v=resto[0]["v"].lstrip(" : "))
                filas.append({"termino": termino, "inl": resto})
            filas.sort(key=lambda f: _sin_acentos(f["termino"]).lower())
            bloques[idx] = {"tipo": "glosario", "filas": filas}


def analizar(texto: str, nombre: str = "<fuente>") -> dict:
    texto = texto.lstrip("﻿")
    meta, cuerpo, base = _front_matter(texto, nombre)
    if meta.get("fecha"):
        try:
            d = datetime.date.fromisoformat(meta["fecha"])
        except ValueError:
            raise ErrorFormato(f"{nombre}: 'fecha' debe ser AAAA-MM-DD")
    else:
        d = datetime.date.today()
    meta["fecha_texto"] = fecha_en_espanol(d)
    meta.setdefault("producto", "Sistema de Pesaje para Faeneo")
    meta.setdefault("empresa", "FRIGORÍFICO EJEMPLO S.R.L.")
    bloques = _Analizador(nombre).bloques(cuerpo, base)
    _glosario(bloques)
    toc = _numerar(bloques)
    # marca las tablas de la hoja de teclas
    en_teclas = False
    for b in bloques:
        if b["tipo"] == "titulo" and b["nivel"] == 1:
            en_teclas = b["especial"] == "teclas"
        elif en_teclas:
            b["en_teclas"] = True
    return {"meta": meta, "bloques": bloques, "toc": toc}


def recorrer_imagenes(bloques: list[dict]):
    """Genera todos los bloques de imagen (incluidos los anidados en pasos y recuadros)."""
    for b in bloques:
        if b["tipo"] == "imagen":
            yield b
        elif b["tipo"] == "lista":
            for it in b["items"]:
                yield from recorrer_imagenes(it["bloques"])
        elif b["tipo"] == "recuadro":
            yield from recorrer_imagenes(b["bloques"])
