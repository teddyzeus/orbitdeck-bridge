// Génère out/gallery.html : toutes les touches dans différents états, pour juger leur lisibilité sans lancer le jeu.
import { spawnSync } from "node:child_process";
import path from "node:path";

const output = path.resolve(import.meta.dirname, "../out/gallery.html");
const result = spawnSync("npx", ["vitest", "run", "tests/gallery.test.ts"], {
	cwd: path.resolve(import.meta.dirname, ".."),
	env: { ...process.env, GALLERY_OUT: output },
	stdio: "inherit",
	shell: true,
});

if (result.status === 0) {
	console.log(`\nGalerie générée : ${output}`);
}

process.exit(result.status ?? 1);
