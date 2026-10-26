#!/usr/bin/env node
// Faux serveur OrbitDeck : imite le mod (protocole v1) pour développer le plugin Stream Deck sans lancer le jeu.
//
//   node fake-server.mjs                       vol simulé sur le port 18765
//   node fake-server.mjs --port 9000
//   node fake-server.mjs --scenario menu       reste dans le menu (aucun véhicule)
//   node fake-server.mjs --scenario cycle      alterne menu → vol → véhicule non contrôlable
//
// Les commandes (setThrottle, adjustThrottle, setSas, toggleSas, setRcs, toggleRcs, setSpeedMode) modifient l'état simulé,
// comme le fera le vrai mod à l'étape T4. Arrêt : Ctrl+C.

import { WebSocketServer } from 'ws';

const args = process.argv.slice(2);
const option = (name) => {
  const index = args.indexOf(name);
  return index >= 0 ? args[index + 1] : undefined;
};

const port = Number(option('--port') ?? 18765);
const scenario = option('--scenario') ?? 'flight';
const MIN_INTERVAL_MS = 100;
const HEARTBEAT_MS = 1000;
const SPEED_MODES = ['surface', 'orbit', 'target'];

const state = {
  status: scenario === 'menu' ? 'noVessel' : 'connected',
  gameState: scenario === 'menu' ? 'MainMenu' : 'FlightView',
  vesselName: 'Kerbal X (simulé)',
  seaLevel: 500,
  climb: 40,
  surfaceSpeed: 120,
  throttle: 0.5,
  speedMode: 'surface',
  sas: { enabled: true, mode: 'StabilityAssist' },
  rcs: false,
  target: null,
};

let seq = 0;
let lastSent = '';

const round = (value, decimals = 2) => Math.round(value * 10 ** decimals) / 10 ** decimals;
const isNumberIn = (value, min, max) => typeof value === 'number' && Number.isFinite(value) && value >= min && value <= max;

function snapshot() {
  const base = { v: 1, type: 'state', seq, status: state.status, gameState: state.gameState };
  if (state.status === 'noVessel') return base;

  return {
    ...base,
    vessel: { name: state.vesselName },
    altitude: { seaLevel: round(state.seaLevel), surface: round(state.seaLevel - 12) },
    speed: {
      mode: state.speedMode,
      surface: round(state.surfaceSpeed),
      orbital: round(state.surfaceSpeed + 175),
      target: state.target === null ? null : round(state.target),
    },
    throttle: round(state.throttle, 3),
    sas: state.sas,
    rcs: { enabled: state.rcs },
  };
}

const wss = new WebSocketServer({ host: '127.0.0.1', port });
const clients = new Set();
let lastSendTime = 0;
let changedSinceSend = true;

function broadcastState(force = false) {
  const now = Date.now();
  const text = JSON.stringify({ ...snapshot(), seq });
  if (!force && now - lastSendTime < MIN_INTERVAL_MS) return;
  lastSendTime = now;
  changedSinceSend = false;
  lastSent = text;
  for (const client of clients) client.send(text);
}

function touch() {
  seq += 1;
  changedSinceSend = true;
}

function reply(socket, message) {
  socket.send(JSON.stringify({ v: 1, ...message }));
}

function error(socket, id, code, message) {
  reply(socket, { type: 'error', ...(id ? { id } : {}), code, message });
}

function handleCommand(socket, message) {
  const { id, name, args: commandArgs = {} } = message;
  if (state.status === 'noVessel') return error(socket, id, 'NO_VESSEL', 'Aucun véhicule actif');
  if (state.status === 'notControllable') return error(socket, id, 'NOT_CONTROLLABLE', 'Véhicule non contrôlable');

  switch (name) {
    case 'setThrottle':
      if (!isNumberIn(commandArgs.value, 0, 1)) return error(socket, id, 'BAD_REQUEST', 'args.value doit être un nombre entre 0 et 1.');
      state.throttle = commandArgs.value;
      break;
    case 'adjustThrottle':
      if (!isNumberIn(commandArgs.delta, -1, 1)) return error(socket, id, 'BAD_REQUEST', 'args.delta doit être un nombre entre -1 et 1.');
      state.throttle = Math.min(1, Math.max(0, state.throttle + commandArgs.delta));
      break;
    case 'setSas':
      if (typeof commandArgs.enabled !== 'boolean') return error(socket, id, 'BAD_REQUEST', 'args.enabled doit être un booléen.');
      state.sas = { ...state.sas, enabled: commandArgs.enabled };
      break;
    case 'toggleSas':
      state.sas = { ...state.sas, enabled: !state.sas.enabled };
      break;
    case 'setRcs':
      if (typeof commandArgs.enabled !== 'boolean') return error(socket, id, 'BAD_REQUEST', 'args.enabled doit être un booléen.');
      state.rcs = commandArgs.enabled;
      break;
    case 'toggleRcs':
      state.rcs = !state.rcs;
      break;
    case 'setSpeedMode':
      if (!SPEED_MODES.includes(commandArgs.mode)) return error(socket, id, 'BAD_REQUEST', 'args.mode : surface, orbit ou target');
      if (commandArgs.mode === 'target' && state.target === null) return error(socket, id, 'REJECTED_BY_GAME', 'Aucune cible sélectionnée');
      state.speedMode = commandArgs.mode;
      break;
    default:
      return error(socket, id, 'UNKNOWN_COMMAND', `Commande « ${name} » inconnue`);
  }

  touch();
  reply(socket, { type: 'ack', id });
  broadcastState(true);
}

wss.on('connection', (socket, request) => {
  if (request.headers.origin) {
    socket.close(1008, 'Origin refusée');
    return;
  }

  clients.add(socket);
  console.log(`Client connecté (${clients.size})`);
  reply(socket, { type: 'hello', mod: '0.0.0-simulé', game: 'simulé' });
  socket.send(JSON.stringify(snapshot()));

  socket.on('message', (data) => {
    let message;
    try {
      message = JSON.parse(data.toString());
    } catch {
      return error(socket, undefined, 'BAD_REQUEST', 'Message JSON invalide');
    }

    if (message?.v !== 1) return error(socket, message?.id, 'BAD_REQUEST', 'Version de protocole non supportée (attendu : 1)');
    if (message.type === 'ping') return reply(socket, { type: 'pong' });
    if (message.type === 'command') {
      if (typeof message.id !== 'string' || !message.id) return error(socket, undefined, 'BAD_REQUEST', 'id requis');
      return handleCommand(socket, message);
    }
    return error(socket, message?.id, 'BAD_REQUEST', `Type de message inconnu : ${message?.type}`);
  });

  socket.on('close', () => {
    clients.delete(socket);
    console.log(`Client déconnecté (${clients.size})`);
  });
});

wss.on('listening', () => console.log(`Faux serveur OrbitDeck sur ws://127.0.0.1:${port}/ (scénario : ${scenario})`));
wss.on('error', (e) => {
  console.error(`Erreur : ${e.message}`);
  process.exit(1);
});

// Simulation : l'altitude monte et descend, la vitesse varie avec la poussée.
let tick = 0;
setInterval(() => {
  tick += 1;

  if (scenario === 'cycle') {
    const phase = Math.floor(tick / 100) % 3; // changement toutes les ~10 s
    const wanted = ['noVessel', 'connected', 'notControllable'][phase];
    if (wanted !== state.status) {
      state.status = wanted;
      state.gameState = wanted === 'noVessel' ? 'KerbalSpaceCenter' : 'FlightView';
      touch();
    }
  }

  if (state.status !== 'noVessel') {
    state.seaLevel += state.climb * 0.1 * (0.2 + state.throttle);
    if (state.seaLevel > 20000 || state.seaLevel < 100) state.climb = -state.climb;
    state.surfaceSpeed = 80 + state.throttle * 400 + Math.sin(tick / 20) * 5;
    state.target = state.seaLevel > 5000 ? state.surfaceSpeed * 0.1 : null;
    if (state.target === null && state.speedMode === 'target') state.speedMode = 'surface';
    touch();
  }

  const now = Date.now();
  if (changedSinceSend || now - lastSendTime >= HEARTBEAT_MS) broadcastState();
}, 100);
