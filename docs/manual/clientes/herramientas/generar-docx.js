"use strict";
// Genera el .docx de un manual a partir del arbol JSON que prepara construir-manuales.py.
// Uso: node generar-docx.js <arbol.json> <salida.docx>
const fs = require("fs");
const path = require("path");
const {
  Document, Packer, Paragraph, TextRun, ImageRun, Table, TableRow, TableCell, WidthType, BorderStyle,
  ShadingType, AlignmentType, HeadingLevel, LevelFormat, Header, Footer, PageNumber, TableOfContents,
  TabStopType, PageBreak, VerticalAlign,
} = require("docx");

const AZUL = "1F4E79";
const ROJO = "D32F2F";
const FUENTE = "Calibri";
const ANCHO_PAGINA = 11906;           // A4
const ALTO_PAGINA = 16838;
const MARGEN_LR = 1134;               // 2 cm
const ANCHO_UTIL = ANCHO_PAGINA - 2 * MARGEN_LR; // 9638 DXA = 17 cm
const SIN_BORDE = { style: BorderStyle.NONE, size: 0, color: "FFFFFF" };
const TODOS_SIN_BORDE = { top: SIN_BORDE, bottom: SIN_BORDE, left: SIN_BORDE, right: SIN_BORDE };

const RECUADROS = {
  important: { color: "B26A00", fondo: "FFF4DC" },
  error: { color: "B71C1C", fondo: "FDE9E9" },
  tip: { color: "1B6E3C", fondo: "E6F4EA" },
};

const [, , arbolRuta, salida] = process.argv;
if (!arbolRuta || !salida) {
  console.error("Uso: node generar-docx.js <arbol.json> <salida.docx>");
  process.exit(2);
}
const ast = JSON.parse(fs.readFileSync(arbolRuta, "utf8").replace(/^﻿/, ""));
const meta = ast.meta;

let instanciaLista = 0;               // cada lista numerada reinicia en 1

// ------------------------------------------------------------------ inline
function runs(tramos, opc = {}) {
  const size = opc.size || 24;
  const out = [];
  for (const t of tramos) {
    const base = { font: FUENTE, size, bold: !!t.b || !!opc.bold, italics: !!t.i, color: opc.color };
    if (t.t === "key") {
      out.push(new TextRun({
        ...base, text: " " + t.v + " ", bold: true, color: "222222",
        size: opc.sizeTecla || size,
        border: { style: BorderStyle.SINGLE, size: 6, color: "8995A1", space: 0 },
        shading: { type: ShadingType.CLEAR, fill: "E9EDF1", color: "auto" },
      }));
    } else {
      out.push(new TextRun({ ...base, text: t.v }));
    }
  }
  return out;
}
const plano = (tramos) => tramos.map((t) => t.v).join("");

// ------------------------------------------------------------------ bloques
function titulo(b) {
  const nivel = b.nivel;
  const heading = [HeadingLevel.HEADING_1, HeadingLevel.HEADING_2, HeadingLevel.HEADING_3][nivel - 1];
  const hijos = [];
  if (b.numero) hijos.push(new TextRun({ text: b.numero + (nivel === 1 ? ".  " : "  "), color: ROJO }));
  for (const t of b.inl) hijos.push(new TextRun({ text: t.v, italics: !!t.i }));
  return new Paragraph({
    heading,
    alignment: b.especial === "teclas" ? AlignmentType.CENTER : AlignmentType.LEFT,
    children: hijos,
  });
}

function parrafo(b, ctx) {
  const o = { size: ctx.size };
  return new Paragraph({
    children: runs(b.inl, o),
    spacing: { after: ctx.enTecla ? 120 : 140, line: 312 },
    alignment: ctx.enTecla ? AlignmentType.CENTER : AlignmentType.LEFT,
    indent: ctx.indent ? { left: ctx.indent } : undefined,
    keepNext: ctx.keepNext || undefined,
  });
}

function imagen(b) {
  const data = fs.readFileSync(b.ruta);
  const tipo = b.ruta.toLowerCase().endsWith(".png") ? "png" : "jpg";
  const w = Math.round(b.ancho_final_cm / 2.54 * 96);
  const h = Math.round(w * b.h_px / b.w_px);
  const out = [
    new Paragraph({
      alignment: AlignmentType.CENTER, keepNext: true, spacing: { before: 120, after: 60 },
      children: [new ImageRun({
        type: tipo, data, transformation: { width: w, height: h },
        altText: { title: b.epigrafe_plano, description: b.epigrafe_plano, name: b.id },
      })],
    }),
    new Paragraph({
      alignment: AlignmentType.CENTER, spacing: { after: b.leyenda && b.leyenda.length ? 40 : 200 }, keepLines: true,
      children: [new TextRun({ text: `Figura ${b.numero_figura}. `, italics: true, font: FUENTE, size: 21, color: "444444" }),
        ...b.epigrafe.map((t) => new TextRun({ text: t.v, italics: true, bold: !!t.b, font: FUENTE, size: 21, color: "444444" }))],
    }),
  ];
  if (b.leyenda && b.leyenda.length) {
    out.push(new Paragraph({
      alignment: AlignmentType.CENTER, spacing: { after: 200 },
      children: b.leyenda.flatMap(([n, txt]) => [
        new TextRun({ text: ` ${n} `, bold: true, color: "FFFFFF", font: FUENTE, size: 19, shading: { type: ShadingType.CLEAR, fill: "E02020", color: "auto" } }),
        new TextRun({ text: " " + txt + "   ", font: FUENTE, size: 21, color: "333333" }),
      ]),
    }));
  }
  return out;
}

// Igual que en el PDF: el texto de un paso se mantiene junto a su figura solo si esta no es muy alta (<= 9 cm).
function siguienteImagenBaja(bl) {
  const sig = bl[1];
  return !!sig && sig.tipo === "imagen" && sig.ancho_final_cm * sig.h_px / sig.w_px <= 9.0;
}

// Largo aproximado de un recuadro: los cortos no se parten entre paginas; los largos si pueden partirse.
function longitud(bloques) {
  let n = 0;
  for (const x of bloques) {
    if (x.tipo === "parrafo") n += plano(x.inl).length;
    else if (x.tipo === "lista") n += x.items.reduce((a, it) => a + longitud(it.bloques) + 40, 0);
    else if (x.tipo === "imagen") n += 10000;
    else n += 300;
  }
  return n;
}

function lista(b, ctx) {
  const out = [];
  const instancia = b.ordenada ? ++instanciaLista : 0;
  const nivel = Math.min(ctx.nivelLista, 2);
  for (const it of b.items) {
    let primero = true;
    const bl = it.bloques;
    if (!bl.length || bl[0].tipo !== "parrafo") {
      out.push(new Paragraph({ children: [], numbering: { reference: b.ordenada ? "pasos" : "vinetas", level: nivel, instance: instancia } }));
      primero = false;
    }
    for (const x of bl) {
      if (primero && x.tipo === "parrafo") {
        out.push(new Paragraph({
          children: runs(x.inl, { size: b.ordenada && nivel === 0 ? 25 : ctx.size }),
          numbering: { reference: b.ordenada ? "pasos" : "vinetas", level: nivel, instance: instancia },
          spacing: { before: b.ordenada ? 80 : 0, after: 100, line: 312 },
          keepNext: (b.ordenada && siguienteImagenBaja(bl)) || undefined,
        }));
        primero = false;
      } else {
        const sangria = (nivel + 1) * 567;
        out.push(...bloque(x, { ...ctx, indent: x.tipo === "lista" ? 0 : sangria, nivelLista: nivel + 1 }));
      }
    }
    // separacion generosa entre pasos
    if (b.ordenada && nivel === 0) out.push(new Paragraph({ spacing: { after: 120 }, children: [], keepLines: true }));
  }
  return out;
}

function recuadro(b, ctx) {
  const c = RECUADROS[b.clase];
  const interior = [
    new Paragraph({
      spacing: { before: 40, after: 80 }, keepNext: true,
      children: [
        new TextRun({ text: ` ${b.icono} `, bold: true, color: "FFFFFF", font: FUENTE, size: 24, shading: { type: ShadingType.CLEAR, fill: c.color, color: "auto" } }),
        new TextRun({ text: "  " + b.etiqueta.toUpperCase(), bold: true, color: c.color, font: FUENTE, size: 22 }),
      ],
    }),
    ...b.bloques.flatMap((x) => bloque(x, { ...ctx, indent: 0, nivelLista: 0 })),
  ];
  // la ultima celda debe terminar en un parrafo
  const tabla = new Table({
    width: { size: ANCHO_UTIL, type: WidthType.DXA }, columnWidths: [ANCHO_UTIL],
    rows: [new TableRow({
      cantSplit: longitud(b.bloques) < 520,
      children: [new TableCell({
        width: { size: ANCHO_UTIL, type: WidthType.DXA },
        shading: { type: ShadingType.CLEAR, fill: c.fondo, color: "auto" },
        borders: { top: SIN_BORDE, bottom: SIN_BORDE, right: SIN_BORDE, left: { style: BorderStyle.SINGLE, size: 36, color: c.color } },
        margins: { top: 100, bottom: 100, left: 220, right: 200 },
        children: interior,
      })],
    })],
  });
  return [tabla, new Paragraph({ spacing: { after: 160 }, children: [], keepLines: true })];
}

function tabla(b, ctx) {
  const n = b.cabecera.length;
  let pesos = b.cabecera.map((c, i) => {
    const largos = [plano(c), ...b.filas.map((f) => plano(f[i]))].map((s) => s.length);
    return Math.min(Math.max(...largos, 8), 60);
  });
  if (ctx.enTecla) pesos = pesos.map((p, i) => (i === 0 ? 14 : Math.max(p, 20)));
  const suma = pesos.reduce((a, c) => a + c, 0);
  let anchos = pesos.map((p) => Math.max(Math.round(ANCHO_UTIL * p / suma), 1100));
  const ajuste = ANCHO_UTIL - anchos.reduce((a, c) => a + c, 0);
  anchos[anchos.length - 1] += ajuste;
  const borde = { style: BorderStyle.SINGLE, size: 4, color: ctx.enTecla ? "9FB2C4" : "C9D3DD" };
  const bordes = { top: borde, bottom: borde, left: borde, right: borde };
  const alin = (a) => (a === "center" ? AlignmentType.CENTER : a === "right" ? AlignmentType.RIGHT : AlignmentType.LEFT);
  const tam = ctx.enTecla ? 28 : 23;
  const filaCab = new TableRow({
    tableHeader: true, cantSplit: true,
    children: b.cabecera.map((c, i) => new TableCell({
      width: { size: anchos[i], type: WidthType.DXA }, borders: bordes,
      shading: { type: ShadingType.CLEAR, fill: AZUL, color: "auto" },
      margins: { top: 80, bottom: 80, left: 120, right: 120 },
      children: [new Paragraph({ alignment: alin(b.alineacion[i]), children: runs(c, { size: ctx.enTecla ? 24 : tam, bold: true, color: "FFFFFF" }) })],
    })),
  });
  const filas = b.filas.map((f, r) => new TableRow({
    cantSplit: true,
    children: f.map((c, i) => new TableCell({
      width: { size: anchos[i], type: WidthType.DXA }, borders: bordes,
      verticalAlign: VerticalAlign.CENTER,
      shading: (ctx.enTecla && i === 0) ? { type: ShadingType.CLEAR, fill: "F3F7FB", color: "auto" }
        : r % 2 === 1 ? { type: ShadingType.CLEAR, fill: "F3F7FB", color: "auto" } : undefined,
      margins: { top: ctx.enTecla ? 120 : 70, bottom: ctx.enTecla ? 120 : 70, left: 120, right: 120 },
      children: [new Paragraph({
        alignment: ctx.enTecla && i === 0 ? AlignmentType.CENTER : alin(b.alineacion[i]),
        children: runs(c, { size: tam, sizeTecla: ctx.enTecla ? 34 : undefined }),
      })],
    })),
  }));
  return [new Table({ width: { size: ANCHO_UTIL, type: WidthType.DXA }, columnWidths: anchos, rows: [filaCab, ...filas] }),
    new Paragraph({ spacing: { after: 120 }, children: [] })];
}

function glosario(b) {
  const l = { style: BorderStyle.SINGLE, size: 4, color: "C9D3DD" };
  const anchos = [2500, ANCHO_UTIL - 2500];
  return [new Table({
    width: { size: ANCHO_UTIL, type: WidthType.DXA }, columnWidths: anchos,
    rows: b.filas.map((f) => new TableRow({
      cantSplit: true,
      children: [
        new TableCell({
          width: { size: anchos[0], type: WidthType.DXA }, borders: { ...TODOS_SIN_BORDE, bottom: l },
          margins: { top: 90, bottom: 90, left: 60, right: 120 },
          children: [new Paragraph({ children: [new TextRun({ text: f.termino, bold: true, color: AZUL, font: FUENTE, size: 24 })] })],
        }),
        new TableCell({
          width: { size: anchos[1], type: WidthType.DXA }, borders: { ...TODOS_SIN_BORDE, bottom: l },
          margins: { top: 90, bottom: 90, left: 60, right: 60 },
          children: [new Paragraph({ children: runs(f.inl, { size: 24 }) })],
        }),
      ],
    })),
  }), new Paragraph({ children: [] })];
}

function bloque(b, ctx) {
  switch (b.tipo) {
    case "titulo": {
      const c2 = ctx;
      if (b.nivel === 1) c2.enTecla = b.especial === "teclas";
      return [titulo(b)];
    }
    case "parrafo": return [parrafo(b, ctx)];
    case "salto": return [new Paragraph({ children: [new PageBreak()] })];
    case "lista": return lista(b, ctx);
    case "recuadro": return recuadro(b, ctx);
    case "imagen": return imagen(b);
    case "tabla": return tabla(b, ctx);
    case "glosario": return glosario(b);
    default: throw new Error("bloque desconocido: " + b.tipo);
  }
}

// ------------------------------------------------------------------ portada
function portada() {
  const c = [];
  const vacio = (antes) => new Paragraph({ spacing: { before: antes }, children: [] });
  c.push(vacio(900));
  if (meta.logo_ruta) {
    const dim = meta.logo_dim;
    const maxW = 260, maxH = 170;
    const k = Math.min(1, maxW / dim[0], maxH / dim[1]);
    c.push(new Paragraph({
      alignment: AlignmentType.CENTER, spacing: { after: 400 },
      children: [new ImageRun({ type: "png", data: fs.readFileSync(meta.logo_ruta),
        transformation: { width: Math.round(dim[0] * k), height: Math.round(dim[1] * k) },
        altText: { title: meta.empresa, description: meta.empresa, name: "logo" } })],
    }));
  } else {
    c.push(new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 500 },
      children: [new TextRun({ text: meta.empresa, bold: true, size: 44, color: AZUL, font: FUENTE })] }));
  }
  c.push(new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 240 },
    children: [new TextRun({ text: meta.titulo, bold: true, size: 80, color: AZUL, font: FUENTE })] }));
  c.push(new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 200 },
    children: [new TextRun({ text: meta.producto, size: 40, color: "333333", font: FUENTE })] }));
  c.push(new Paragraph({ alignment: AlignmentType.CENTER, spacing: { before: 200, after: 2400 },
    children: [new TextRun({ text: `   ${meta.rol}   `, bold: true, size: 30, color: "FFFFFF", font: FUENTE,
      shading: { type: ShadingType.CLEAR, fill: AZUL, color: "auto" } })] }));
  c.push(new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 80 },
    children: [new TextRun({ text: `Versión ${meta.version}`, bold: true, size: 30, font: FUENTE })] }));
  c.push(new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 80 },
    children: [new TextRun({ text: meta.fecha_texto, size: 28, color: "555555", font: FUENTE })] }));
  c.push(new Paragraph({ alignment: AlignmentType.CENTER,
    children: [new TextRun({ text: meta.empresa, size: 24, color: "555555", font: FUENTE })] }));
  return c;
}

// ------------------------------------------------------------------ documento
const ctxBase = { size: 24, indent: 0, nivelLista: 0, enTecla: false };
const cuerpo = [];
for (const b of ast.bloques) cuerpo.push(...bloque(b, ctxBase));

const tituloIndice = new Paragraph({
  spacing: { after: 240 },
  border: { bottom: { style: BorderStyle.SINGLE, size: 18, color: AZUL, space: 4 } },
  children: [new TextRun({ text: "Contenido", bold: true, size: 52, color: AZUL, font: FUENTE })],
});
const nota = new Paragraph({
  spacing: { after: 120 },
  children: [new TextRun({ text: "Si no ve los números de página, haga clic derecho sobre el índice y elija «Actualizar campo».", italics: true, size: 18, color: "777777", font: FUENTE })],
});

const numLevels = (fmt, textos, bulletChars) => [0, 1, 2].map((i) => ({
  level: i, format: fmt, text: textos[i], alignment: AlignmentType.LEFT,
  style: {
    paragraph: { indent: { left: 567 * (i + 1), hanging: 454 } },
    run: fmt === LevelFormat.DECIMAL ? { bold: true, color: AZUL } : { color: AZUL },
  },
}));

const doc = new Document({
  creator: "Sistema de Pesaje para Faeneo",
  title: `${meta.titulo} - ${meta.producto}`,
  description: `Versión ${meta.version}`,
  features: { updateFields: true },
  styles: {
    default: { document: { run: { font: FUENTE, size: 24, language: { value: "es-ES" } }, paragraph: { spacing: { line: 312 } } } },
    paragraphStyles: [
      { id: "Heading1", name: "Heading 1", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 52, bold: true, color: AZUL, font: FUENTE },
        paragraph: { spacing: { before: 0, after: 280, line: 276 }, outlineLevel: 0, pageBreakBefore: true, keepNext: true,
          border: { bottom: { style: BorderStyle.SINGLE, size: 18, color: AZUL, space: 4 } } } },
      { id: "Heading2", name: "Heading 2", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 36, bold: true, color: AZUL, font: FUENTE },
        paragraph: { spacing: { before: 420, after: 160, line: 276 }, outlineLevel: 1, keepNext: true } },
      { id: "Heading3", name: "Heading 3", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 28, bold: true, color: "17354F", font: FUENTE },
        paragraph: { spacing: { before: 320, after: 120, line: 276 }, outlineLevel: 2, keepNext: true } },
    ],
  },
  numbering: {
    config: [
      { reference: "pasos", levels: numLevels(LevelFormat.DECIMAL, ["%1.", "%2.", "%3."]) },
      { reference: "vinetas", levels: numLevels(LevelFormat.BULLET, ["•", "–", "•"]) },
    ],
  },
  sections: [
    {
      properties: { page: { size: { width: ANCHO_PAGINA, height: ALTO_PAGINA }, margin: { top: 1134, bottom: 1134, left: MARGEN_LR, right: MARGEN_LR } } },
      children: portada(),
    },
    {
      properties: { page: { size: { width: ANCHO_PAGINA, height: ALTO_PAGINA }, margin: { top: 1418, bottom: 1247, left: MARGEN_LR, right: MARGEN_LR, header: 600, footer: 500 } } },
      headers: { default: new Header({ children: [new Paragraph({
        tabStops: [{ type: TabStopType.RIGHT, position: ANCHO_UTIL }],
        border: { bottom: { style: BorderStyle.SINGLE, size: 6, color: "C9D3DD", space: 4 } },
        children: [
          new TextRun({ text: `${meta.titulo} · ${meta.producto}`, size: 19, color: "555555", font: FUENTE, bold: true }),
          new TextRun({ text: `\tVersión ${meta.version}`, size: 19, color: "555555", font: FUENTE }),
        ] })] }) },
      footers: { default: new Footer({ children: [new Paragraph({
        alignment: AlignmentType.CENTER,
        children: [
          new TextRun({ text: "Página ", size: 20, color: "555555", font: FUENTE }),
          new TextRun({ children: [PageNumber.CURRENT], size: 20, color: "555555", font: FUENTE }),
          new TextRun({ text: " de ", size: 20, color: "555555", font: FUENTE }),
          new TextRun({ children: [PageNumber.TOTAL_PAGES], size: 20, color: "555555", font: FUENTE }),
        ] })] }) },
      children: [
        tituloIndice,
        new TableOfContents("Contenido", { hyperlink: true, headingStyleRange: "1-2" }),
        nota,
        ...cuerpo,
      ],
    },
  ],
});

// Word actualiza el indice al abrir (features.updateFields), pero en "Vista protegida" no lo hace:
// por eso se rellena de antemano con los titulos (sin numeros de pagina).
const xmlEsc = (t) => t.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
function rellenarIndice(xml) {
  const ini = '<w:fldChar w:fldCharType="separate"/></w:r></w:p>';
  const i = xml.indexOf(ini);
  const fin = '<w:p><w:r><w:fldChar w:fldCharType="end"/></w:r></w:p>';
  const j = xml.indexOf(fin, i);
  if (i < 0 || j < 0) return xml;
  const filas = ast.toc.map((e) => {
    const txt = (e.numero ? e.numero + (e.nivel === 1 ? ".  " : "  ") : "") + e.texto;
    const ppr = `<w:pPr><w:spacing w:before="${e.nivel === 1 ? 160 : 0}" w:after="40"/><w:ind w:left="${e.nivel === 1 ? 0 : 400}"/></w:pPr>`;
    const rpr = `<w:rPr><w:rFonts w:ascii="Calibri" w:hAnsi="Calibri" w:cs="Calibri"/>${e.nivel === 1 ? "<w:b/><w:bCs/>" : ""}<w:sz w:val="${e.nivel === 1 ? 25 : 23}"/></w:rPr>`;
    return { ppr, run: `<w:r>${rpr}<w:t xml:space="preserve">${xmlEsc(txt)}</w:t></w:r>` };
  });
  const parrafos = filas.map((f, k) => {
    const cierre = k === filas.length - 1 ? '<w:r><w:fldChar w:fldCharType="end"/></w:r>' : "";
    return `<w:p>${f.ppr}${f.run}${cierre}</w:p>`;
  }).join("");
  // el primer parrafo conserva el inicio del campo; los demas siguen dentro del resultado
  const inicioCampo = xml.slice(xml.lastIndexOf("<w:p>", i), i + ini.length - "</w:p>".length);
  const empieza = xml.lastIndexOf("<w:p>", i);
  if (!filas.length) return xml;
  const resto = parrafos.slice(parrafos.indexOf("</w:p>") + "</w:p>".length);
  const primeroXml = `${inicioCampo.replace("<w:p>", "<w:p>" + filas[0].ppr)}${filas[0].run}${filas.length === 1 ? '<w:r><w:fldChar w:fldCharType="end"/></w:r>' : ""}</w:p>`;
  return xml.slice(0, empieza) + primeroXml + resto + xml.slice(j + fin.length);
}

Packer.toBuffer(doc).then(async (buf) => {
  const JSZip = require("jszip");
  const zip = await JSZip.loadAsync(buf);
  const xml = await zip.file("word/document.xml").async("string");
  zip.file("word/document.xml", rellenarIndice(xml));
  const salidaBuf = await zip.generateAsync({ type: "nodebuffer", compression: "DEFLATE" });
  fs.writeFileSync(salida, salidaBuf);
  console.log("Word generado: " + path.basename(salida) + " (" + Math.round(salidaBuf.length / 1024) + " KB)");
});
