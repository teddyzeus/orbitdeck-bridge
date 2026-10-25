import streamDeck from "@elgato/streamdeck";

import { AltitudeAction } from "./actions/altitude.js";
import { SpeedAction } from "./actions/speed.js";
import { StatusAction } from "./actions/status.js";
import { RcsAction, SasAction } from "./actions/switches.js";
import { ThrottleAction } from "./actions/throttle.js";
import { ThrottleDownAction, ThrottleSetAction, ThrottleUpAction } from "./actions/throttleControls.js";
import { urlFor, type GlobalSettings } from "./connection/address.js";
import { ModClient } from "./connection/ModClient.js";
import { Store } from "./state/Store.js";

// Un seul client et un seul état pour toute la durée de vie du plugin : les touches s'y abonnent une fois.
const client = new ModClient({
	url: urlFor(undefined),
	log: (message) => streamDeck.logger.debug(message),
});
const store = new Store(client);
client.onConnectionChange((state) => streamDeck.logger.info(`Connexion au jeu : ${state}`));

const dependencies = { store, client, language: streamDeck.info.application.language };
streamDeck.actions.registerAction(new AltitudeAction(dependencies));
streamDeck.actions.registerAction(new SpeedAction(dependencies));
streamDeck.actions.registerAction(new ThrottleAction(dependencies));
streamDeck.actions.registerAction(new StatusAction(dependencies));
streamDeck.actions.registerAction(new SasAction(dependencies));
streamDeck.actions.registerAction(new RcsAction(dependencies));
streamDeck.actions.registerAction(new ThrottleSetAction(dependencies));
streamDeck.actions.registerAction(new ThrottleUpAction(dependencies));
streamDeck.actions.registerAction(new ThrottleDownAction(dependencies));

await streamDeck.connect();

// L'adresse vient des réglages globaux (modifiables depuis l'interface des touches) ; port 8765 par défaut.
client.reconfigure(urlFor(await streamDeck.settings.getGlobalSettings<GlobalSettings>()));
client.start();
streamDeck.logger.info(`Connexion au mod StreamPlug sur ${client.url}`);

streamDeck.settings.onDidReceiveGlobalSettings<GlobalSettings>((ev) => {
	client.reconfigure(urlFor(ev.settings));
	streamDeck.logger.info(`Connexion au mod StreamPlug sur ${client.url}`);
});
