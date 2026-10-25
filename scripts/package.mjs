// Fabrique les deux fichiers de release dans release/ :
//   StreamPlug-Mod-vX.Y.Z.zip      (dossier streamplug/ à copier dans <jeu>\mods\)
//   com.teddyzeus.streamplug.streamDeckPlugin
// Prérequis : SDK .NET, Node, `npm ci` dans streamdeck-plugin/, KSP2_DIR (ou Directory.Build.props.user) pour compiler le mod.
import { spawnSync } from "node:child_process";
import { cpSync, existsSync, mkdirSync, readFileSync, rmSync } from "node:fs";
import path from "node:path";

const root = path.resolve(import.meta.dirname, "..");
const releaseDir = path.join(root, "release");
const plugin = path.join(root, "streamdeck-plugin");
const modDir = path.join(root, "mod");
const swinfo = JSON.parse(readFileSync(path.join(modDir, "package/swinfo.json"), "utf8"));
const version = swinfo.version;

function run(command, args, cwd) {
	console.log(`\n> ${command} ${args.join(" ")}`);
	const result = spawnSync(command, args, { cwd, stdio: "inherit", shell: process.platform === "win32" });
	if (result.status !== 0) {
		throw new Error(`Échec : ${command} ${args.join(" ")}`);
	}
}

// Les trois versions (mod, plugin, manifeste) doivent concorder.
const manifestPath = path.join(plugin, "com.teddyzeus.streamplug.sdPlugin/manifest.json");
const manifestVersion = JSON.parse(readFileSync(manifestPath, "utf8")).Version;
if (manifestVersion !== `${version}.0`) {
	throw new Error(`Versions différentes : swinfo.json ${version}, manifest.json ${manifestVersion} (attendu ${version}.0)`);
}

rmSync(releaseDir, { recursive: true, force: true });
mkdirSync(releaseDir, { recursive: true });

// --- Mod ---
run("dotnet", ["build", "src/StreamPlug.Mod/StreamPlug.Mod.csproj", "-c", "Release", "--nologo", "-v", "q", "-nowarn:MSB3277"], modDir);
const built = path.join(modDir, "src/StreamPlug.Mod/bin/Release/netstandard2.1");
const stage = path.join(releaseDir, "stage", "streamplug");
mkdirSync(stage, { recursive: true });
for (const file of ["StreamPlug.Mod.dll"]) {
	if (!existsSync(path.join(built, file))) throw new Error(`Fichier compilé introuvable : ${path.join(built, file)}`);
	cpSync(path.join(built, file), path.join(stage, file));
}
cpSync(path.join(modDir, "package/swinfo.json"), path.join(stage, "swinfo.json"));
cpSync(path.join(modDir, "package/streamplug-config.json"), path.join(stage, "streamplug-config.json"));

const modZip = `StreamPlug-Mod-v${version}.zip`;
// `tar.exe` de Windows (bsdtar ; pas celui de Git Bash, qui lit « D: » comme un hôte) écrit un zip aux chemins en « / », lisible partout.
run(path.join(process.env.SystemRoot ?? "C:/Windows", "System32", "tar.exe"), ["-a", "-c", "-f", path.join(releaseDir, modZip), "-C", path.join(releaseDir, "stage"), "streamplug"], root);
rmSync(path.join(releaseDir, "stage"), { recursive: true, force: true });

// --- Plugin Stream Deck ---
run("npm", ["run", "build"], plugin);
run("npx", ["streamdeck", "validate", "com.teddyzeus.streamplug.sdPlugin"], plugin);
run("npx", ["streamdeck", "pack", "com.teddyzeus.streamplug.sdPlugin", "--output", releaseDir, "--force", "--no-update-check"], plugin);

console.log(`\nRelease v${version} prête dans ${releaseDir}`);
