// Régénère les PNG du plugin (imgs/plugin) depuis assets/*.svg avec Edge en mode sans interface.
import { spawnSync } from "node:child_process";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import { pathToFileURL } from "node:url";

const root = path.resolve(import.meta.dirname, "..");
const out = path.join(root, "com.teddyzeus.streamplug.sdPlugin/imgs/plugin");
const edge = process.env.EDGE_PATH ?? "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
const jobs = [
	["marketplace.svg", "marketplace.png", 288, "0f1218"],
	["marketplace.svg", "marketplace@2x.png", 512, "0f1218"],
	["category.svg", "category-icon.png", 28, "00000000"],
	["category.svg", "category-icon@2x.png", 56, "00000000"],
];

const tmp = mkdtempSync(path.join(os.tmpdir(), "streamplug-icons-"));
try {
	for (const [source, target, size, background] of jobs) {
		const svg = readFileSync(path.join(root, "assets", source), "utf8");
		const page = path.join(tmp, "page.html");
		const html = `<!doctype html><style>html,body{margin:0;background:transparent}svg{display:block;width:${size}px;height:${size}px}</style>${svg}`;
		writeFileSync(page, html);
		const result = spawnSync(edge, ["--headless", "--disable-gpu", "--hide-scrollbars", `--default-background-color=${background.length === 6 ? background + "ff" : background}`,
			`--window-size=${size},${size}`, `--screenshot=${path.join(out, target)}`, pathToFileURL(page).href], { stdio: "inherit" });
		if (result.error || result.status === null) throw new Error(`Edge a échoué pour ${target} : ${result.error?.message ?? result.signal}`);
	}
} finally {
	rmSync(tmp, { recursive: true, force: true });
}

