#!/usr/bin/env node
// Client de test du mod StreamPlug : se connecte, affiche les messages reçus et permet d'envoyer des commandes.
//
//   node client.mjs                      affiche un état à chaque changement de poussée, SAS, RCS, mode, véhicule…
//   node client.mjs --all                affiche tous les états (10 par seconde)
//   node client.mjs --raw                affiche le JSON brut
//   node client.mjs --port 9000          autre port
//   node client.mjs --send '{"v":1,"type":"ping"}'   envoie un message puis continue d'écouter
//   node client.mjs --once               quitte après le premier état reçu
//
// En mode interactif, tapez une commande puis Entrée :
//   ping | throttle 0.5 | throttle +0.1 | sas on|off|toggle | rcs on|off|toggle | speed surface|orbit|target
//   ou directement un objet JSON.

import readline from 'node:readline';
import WebSocket from 'ws';

const args = process.argv.slice(2);
const option = (name) => {
  const index = args.indexOf(name);
  return index >= 0 ? args[index + 1] : undefined;
};

const port = Number(option('--port') ?? 8765);
const raw = args.includes('--raw');
const showAll = args.includes('--all');
const once = args.includes('--once');
const initialMessage = option('--send');
const url = `ws://127.0.0.1:${port}/`;

let nextId = 1;
const newId = () => `cli-${nextId++}`;

function formatNumber(value, unit, digits = 1) {
  return value === null || value === undefined ? '-' : `${Number(value).toFixed(digits)}${unit}`;
}

function describe(message) {
  switch (message.type) {
    case 'hello':
      return `HELLO mod=${message.mod} jeu=${message.game}`;
    case 'state': {
      if (message.status === 'noVessel') {
        return `#${message.seq} AUCUN VÉHICULE (${message.gameState ?? '?'})`;
      }
      const speed = message.speed ?? {};
      return [
        `#${message.seq} ${message.status} '${message.vessel?.name ?? '?'}'`,
        `alt mer=${formatNumber(message.altitude?.seaLevel, ' m')} sol=${formatNumber(message.altitude?.surface, ' m')}`,
        `vitesse(${speed.mode}) sol=${formatNumber(speed.surface, '')} orb=${formatNumber(speed.orbital, '')} cible=${formatNumber(speed.target, '')}`,
        `gaz=${Math.round((message.throttle ?? 0) * 100)}%`,
        `SAS=${message.sas?.enabled ? 'on' : 'off'}(${message.sas?.mode ?? '-'})`,
        `RCS=${message.rcs?.enabled ? 'on' : 'off'}`,
      ].join(' | ');
    }
    case 'ack':
      return `ACK ${message.id}`;
    case 'error':
      return `ERREUR ${message.code}${message.id ? ` (${message.id})` : ''} : ${message.message}`;
    case 'pong':
      return 'PONG';
    default:
      return JSON.stringify(message);
  }
}

function toMessage(line) {
  const text = line.trim();
  if (text.startsWith('{')) {
    return JSON.parse(text);
  }

  const [word, value] = text.split(/\s+/);
  const command = (name, commandArgs) => ({ v: 1, type: 'command', id: newId(), name, ...(commandArgs ? { args: commandArgs } : {}) });

  switch (word) {
    case 'ping':
      return { v: 1, type: 'ping' };
    case 'throttle':
      return /^[+-]/.test(value ?? '')
        ? command('adjustThrottle', { delta: Number(value) })
        : command('setThrottle', { value: Number(value) });
    case 'sas':
      return value === 'toggle' ? command('toggleSas') : command('setSas', { enabled: value === 'on' });
    case 'rcs':
      return value === 'toggle' ? command('toggleRcs') : command('setRcs', { enabled: value === 'on' });
    case 'speed':
      return command('setSpeedMode', { mode: value });
    default:
      throw new Error(`Commande inconnue : « ${text} »`);
  }
}

const socket = new WebSocket(url);
const send = (message) => {
  const text = typeof message === 'string' ? message : JSON.stringify(message);
  console.log(`→ ${text}`);
  socket.send(text);
};

socket.on('open', () => {
  console.log(`Connecté à ${url}`);
  if (initialMessage) {
    send(initialMessage);
  }
});

// Par défaut, un état n'est affiché que si un élément « discret » a changé (pas l'altitude ni les vitesses,
// qui varient en continu). --all affiche tous les états.
let lastDiscrete = null;
function discreteKey(message) {
  return JSON.stringify([
    message.status,
    message.gameState,
    message.vessel?.name,
    message.speed?.mode,
    message.speed?.target === null || message.speed?.target === undefined ? 'sans-cible' : 'cible',
    Math.round((message.throttle ?? 0) * 100),
    message.sas?.enabled,
    message.sas?.mode,
    message.rcs?.enabled,
  ]);
}

function shouldPrint(message) {
  if (showAll || message.type !== 'state') return true;
  const key = discreteKey(message);
  if (key === lastDiscrete) return false;
  lastDiscrete = key;
  return true;
}

socket.on('message', (data) => {
  const text = data.toString();
  let message;
  try {
    message = JSON.parse(text);
  } catch {
    console.log(`(message non JSON) ${text}`);
    return;
  }

  if (shouldPrint(message)) {
    console.log(raw ? text : describe(message));
  }

  if (once && message.type === 'state') {
    socket.close();
  }
});

socket.on('close', (code, reason) => {
  console.log(`Déconnecté (${code}${reason?.length ? ` ${reason}` : ''})`);
  process.exit(0);
});

socket.on('error', (error) => {
  console.error(`Erreur : ${error.message}`);
  process.exit(1);
});

if (process.stdin.isTTY) {
  readline.createInterface({ input: process.stdin }).on('line', (line) => {
    if (!line.trim()) return;
    try {
      send(toMessage(line));
    } catch (error) {
      console.error(error.message);
    }
  });
}
