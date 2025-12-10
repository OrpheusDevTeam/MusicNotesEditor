// script.mjs
import fs from "fs";
import createVerovioModule from "verovio/wasm";
import { VerovioToolkit } from "verovio/esm";
import { PDFDocument } from "pdf-lib";
import { createCanvas, loadImage } from "canvas";

async function musicxmlToPdf(inputMusicXmlPath, outputPdfPath) {
    // Initialize Verovio WASM
    const VerovioModule = await createVerovioModule();
    const vrv = new VerovioToolkit(VerovioModule);

    vrv.setOptions({
        condense: "none"
    });
    // Load MusicXML
    const score = fs.readFileSync(inputMusicXmlPath, "utf-8");
    if (!vrv.loadData(score)) {
        throw new Error("Failed to load MusicXML file");
    }

    const pageCount = vrv.getPageCount();
    if (pageCount === 0) throw new Error("MusicXML resulted in 0 pages");

    // Create PDF
    const pdfDoc = await PDFDocument.create();

    for (let pageNum = 1; pageNum <= pageCount; pageNum++) {
        const svgData = vrv.renderToSVG(pageNum, { adjustPageHeight: true });

        // Convert SVG → PNG using canvas
        const canvas = createCanvas(2100, 2970);
        const ctx = canvas.getContext("2d");

        const svgBuffer = Buffer.from(svgData, "utf-8");
        const img = await loadImage(`data:image/svg+xml;base64,${svgBuffer.toString("base64")}`);
        ctx.drawImage(img, 0, 0, 2100, 2970);

        const pngBytes = canvas.toBuffer("image/png");

        // Add page to PDF
        const pdfPage = pdfDoc.addPage([2100, 2970]);
        const pngImage = await pdfDoc.embedPng(pngBytes);
        pdfPage.drawImage(pngImage, { x: 0, y: 0, width: 2100, height: 2970 });
    }

    // Save PDF
    const pdfBytes = await pdfDoc.save();
    fs.writeFileSync(outputPdfPath, pdfBytes);
    console.log(`Created PDF: ${outputPdfPath}`);
}

// Parse command line arguments
function parseArguments() {
    const args = process.argv.slice(2);
    
    if (args.length < 2) {
        console.error("Usage: node script.mjs <input-musicxml-file> <output-pdf-file>");
        console.error("Example: node script.mjs input.musicxml output.pdf");
        process.exit(1);
    }
    
    const inputPath = args[0];
    const outputPath = args[1];
    
    // Basic validation
    if (!fs.existsSync(inputPath)) {
        console.error(`Error: Input file '${inputPath}' does not exist`);
        process.exit(1);
    }
    
    if (!inputPath.toLowerCase().endsWith('.musicxml') && 
        !inputPath.toLowerCase().endsWith('.xml')) {
        console.warn("Warning: Input file doesn't have .musicxml or .xml extension");
    }
    
    if (!outputPath.toLowerCase().endsWith('.pdf')) {
        console.warn("Warning: Output file doesn't have .pdf extension");
    }
    
    return { inputPath, outputPath };
}

// Main execution
try {
    const { inputPath, outputPath } = parseArguments();
    console.log(`Converting ${inputPath} to ${outputPath}...`);
    
    musicxmlToPdf(inputPath, outputPath).catch((err) => {
        console.error("Error:", err);
        process.exit(1);
    });
} catch (error) {
    console.error("Error:", error.message);
    process.exit(1);
}