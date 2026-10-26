// Fabrique release/OrbitDeck-Bridge-vX.Y.Z.zip (dossier orbitdeck/ à copier dans <jeu>\mods\).
// Prérequis : SDK .NET, KSP2_DIR (ou mod/Directory.Build.props.user) pour compiler le mod.
import { spawnSync } from "node:child_process";
import { cpSync, existsSync, mkdirSync, readFileSync, rmSync } from "node:fs";
import path from "node:path";

const root = path.resolve(import.meta.dirname, "..");
const releaseDir = path.join(root, "release");
const modDir = path.join(root, "mod");
const version = JSON.parse(readFileSync(path.join(modDir, "package/swinfo.json"), "utf8")).version;

function run(command, args, cwd) {
	console.log(`\n> ${command} ${args.join(" ")}`);
	const result = spawnSync(command, args, { cwd, stdio: "inherit" });
	if (result.status !== 0) {
		throw new Error(`Échec : ${command} ${args.join(" ")}`);
	}
}

rmSync(releaseDir, { recursive: true, force: true });
mkdirSync(releaseDir, { recursive: true });

run("dotnet", ["build", "src/OrbitDeck.Mod/OrbitDeck.Mod.csproj", "-c", "Release", "--nologo", "-v", "q", "-nowarn:MSB3277"], modDir);
const built = path.join(modDir, "src/OrbitDeck.Mod/bin/Release/netstandard2.1/OrbitDeck.Mod.dll");
if (!existsSync(built)) throw new Error(`Fichier compilé introuvable : ${built}`);

const stage = path.join(releaseDir, "stage", "orbitdeck");
mkdirSync(stage, { recursive: true });
cpSync(built, path.join(stage, "OrbitDeck.Mod.dll"));
cpSync(path.join(modDir, "package/swinfo.json"), path.join(stage, "swinfo.json"));
cpSync(path.join(modDir, "package/orbitdeck-config.json"), path.join(stage, "orbitdeck-config.json"));

// `tar.exe` de Windows (bsdtar ; pas celui de Git Bash, qui lit « D: » comme un hôte) écrit un zip aux chemins en « / ».
const tar = process.platform === "win32" ? path.join(process.env.SystemRoot ?? "C:/Windows", "System32", "tar.exe") : "zip";
const zip = path.join(releaseDir, `OrbitDeck-Bridge-v${version}.zip`);
if (process.platform === "win32") {
	run(tar, ["-a", "-c", "-f", zip, "-C", path.join(releaseDir, "stage"), "orbitdeck"], root);
} else {
	run("zip", ["-r", zip, "orbitdeck"], path.join(releaseDir, "stage"));
}
rmSync(path.join(releaseDir, "stage"), { recursive: true, force: true });
console.log(`\nRelease v${version} prête dans ${releaseDir}`);
