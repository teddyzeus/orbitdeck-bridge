#!/bin/sh
# Lance les tests du mod dans un conteneur Linux (le contrôle d'application de Windows bloque les DLL fraîchement compilées).
# Depuis le dossier du dépôt, avec Docker Desktop démarré :
#   docker run --rm -v "<dépôt>:/src:ro" -v "<jeu KSP 2>\KSP2_x64_Data\Managed:/ksp2/KSP2_x64_Data/Managed:ro" \
#     -v "<dépôt>\scripts:/scripts:ro" mcr.microsoft.com/dotnet/sdk:8.0 sh /scripts/test-docker.sh [filtre]
# Le dépôt est copié (sans bin ni obj) : rien n'est écrit dans le dépôt ni dans le dossier du jeu (NoDeploy).
set -e
mkdir -p /work && cd /src
tar cf - --exclude='*/bin' --exclude='*/obj' --exclude=node_modules --exclude=.git mod protocol | tar xf - -C /work
cd /work/mod
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
if [ -n "$1" ]; then
  dotnet test tests/OrbitDeck.Mod.Tests -p:Ksp2Dir=/ksp2 -p:NoDeploy=true --filter "FullyQualifiedName~$1" 2>&1 | tail -15
else
  dotnet test tests/OrbitDeck.Mod.Tests -p:Ksp2Dir=/ksp2 -p:NoDeploy=true 2>&1 | tail -15
fi
