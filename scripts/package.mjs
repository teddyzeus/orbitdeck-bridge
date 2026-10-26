// Fabrique les archives de release dans release/ :
//   OrbitDeck-Bridge-KSP2-vX.Y.Z.zip   (dossier orbitdeck/ à copier dans <KSP 2>\mods\)
//   OrbitDeck-Bridge-KSP1-vX.Y.Z.zip   (dossier GameData/ à fusionner dans le dossier de KSP 1)
// Prérequis : SDK .NET, KSP1_DIR / KSP2_DIR (ou mod/Directory.Build.props.user) pour compiler contre les DLL des jeux.
import { spawnSync } from "node:child_process";
import { cpSync, existsSync, mkdirSync, readFileSync, rmSync } from "node:fs";
import path from "node:path";

const root = path.resolve(import.meta.dirname, "..");
const releaseDir = path.join(root, "release");
const stageDir = path.join(releaseDir, "stage");
const modDir = path.join(root, "mod");
const version = JSON.parse(readFileSync(path.join(modDir, "package/swinfo.json"), "utf8")).version;

function run(command, args, cwd) {
	console.log(`\n> ${command} ${args.join(" ")}`);
	const result = spawnSync(command, args, { cwd, stdio: "inherit" });
	if (result.status !== 0) {
		throw new Error(`Échec : ${command} ${args.join(" ")}`);
	}
}

function csprojVersion(project) {
	const match = /<Version>([^<]+)<\/Version>/.exec(readFileSync(path.join(modDir, "src", project, `${project}.csproj`), "utf8"));
	return match?.[1];
}

function need(file) {
	if (!existsSync(file)) throw new Error(`Fichier introuvable : ${file}`);
	return file;
}

// Les versions du mod KSP 2, du mod KSP 1 et de swinfo.json doivent concorder.
for (const project of ["OrbitDeck.Mod", "OrbitDeck.Ksp1"]) {
	if (csprojVersion(project) !== version) {
		throw new Error(`Versions différentes : swinfo.json ${version}, ${project}.csproj ${csprojVersion(project)}`);
	}
}

// `tar.exe` de Windows (bsdtar ; pas celui de Git Bash, qui lit « D: » comme un hôte) écrit un zip aux chemins en « / ».
function zip(archive, baseDir, entry) {
	if (process.platform === "win32") {
		run(path.join(process.env.SystemRoot ?? "C:/Windows", "System32", "tar.exe"), ["-a", "-c", "-f", archive, "-C", baseDir, entry], root);
	} else {
		run("zip", ["-r", archive, entry], baseDir);
	}
}

const license = need(path.join(root, "LICENSE"));
const newtonsoftLicense = need(path.join(process.env.USERPROFILE ?? process.env.HOME ?? "", ".nuget/packages/newtonsoft.json/13.0.3/LICENSE.md"));

rmSync(releaseDir, { recursive: true, force: true });
mkdirSync(stageDir, { recursive: true });

// --- KSP 2 (Redux) ---
run("dotnet", ["build", "src/OrbitDeck.Mod/OrbitDeck.Mod.csproj", "-c", "Release", "--nologo", "-v", "q", "-nowarn:MSB3277"], modDir);
const ksp2 = path.join(stageDir, "ksp2", "orbitdeck");
mkdirSync(ksp2, { recursive: true });
cpSync(need(path.join(modDir, "src/OrbitDeck.Mod/bin/Release/netstandard2.1/OrbitDeck.Mod.dll")), path.join(ksp2, "OrbitDeck.Mod.dll"));
cpSync(path.join(modDir, "package/swinfo.json"), path.join(ksp2, "swinfo.json"));
cpSync(path.join(modDir, "package/orbitdeck-config.json"), path.join(ksp2, "orbitdeck-config.json"));
cpSync(license, path.join(ksp2, "LICENSE.txt"));
zip(path.join(releaseDir, `OrbitDeck-Bridge-KSP2-v${version}.zip`), path.join(stageDir, "ksp2"), "orbitdeck");

// --- KSP 1 ---
run("dotnet", ["build", "src/OrbitDeck.Ksp1/OrbitDeck.Ksp1.csproj", "-c", "Release", "--nologo", "-v", "q"], modDir);
const built1 = path.join(modDir, "src/OrbitDeck.Ksp1/bin/Release/net472");
const ksp1 = path.join(stageDir, "ksp1", "GameData", "OrbitDeck");
mkdirSync(path.join(ksp1, "Plugins"), { recursive: true });
cpSync(need(path.join(built1, "OrbitDeck.Ksp1.dll")), path.join(ksp1, "Plugins", "OrbitDeck.Ksp1.dll"));
cpSync(need(path.join(built1, "Newtonsoft.Json.dll")), path.join(ksp1, "Plugins", "Newtonsoft.Json.dll"));
cpSync(path.join(modDir, "package/orbitdeck-config.json"), path.join(ksp1, "Plugins", "orbitdeck-config.json"));
cpSync(license, path.join(ksp1, "LICENSE.txt"));
cpSync(newtonsoftLicense, path.join(ksp1, "LICENSE-Newtonsoft.Json.txt"));
zip(path.join(releaseDir, `OrbitDeck-Bridge-KSP1-v${version}.zip`), path.join(stageDir, "ksp1"), "GameData");

rmSync(stageDir, { recursive: true, force: true });
console.log(`\nRelease v${version} prête dans ${releaseDir}`);
