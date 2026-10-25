import { action, type KeyDownEvent } from "@elgato/streamdeck";

import { throttleSetFace, throttleStepFace, type FaceContext } from "../rendering/faces.js";
import type { KeyFace } from "../rendering/renderFace.js";
import { DisplayAction } from "./DisplayAction.js";
import { numberSetting } from "./settings.js";

export type ThrottleSetSettings = {
	/** Poussée à appliquer, en pourcentage (0 par défaut). */
	percent?: number | string;
};

export type ThrottleStepSettings = {
	/** Taille du pas, en points de pourcentage (10 par défaut). */
	step?: number | string;
};

const percentOf = (settings: ThrottleSetSettings): number => numberSetting(settings.percent, 0, 0, 100);
const stepOf = (settings: ThrottleStepSettings): number => numberSetting(settings.step, 10, 1, 100);

/** Fixe la poussée à une valeur choisie dans l'interface de la touche (0, 25, 50, 75 ou 100 %). */
@action({ UUID: "com.teddyzeus.streamplug.throttle-set" })
export class ThrottleSetAction extends DisplayAction<ThrottleSetSettings> {
	protected override face(context: FaceContext, settings: ThrottleSetSettings): KeyFace {
		return throttleSetFace(context, percentOf(settings));
	}

	override async onKeyDown(ev: KeyDownEvent<ThrottleSetSettings>): Promise<void> {
		await this.command(ev.action, "setThrottle", { value: percentOf(ev.payload.settings) / 100 });
	}
}

/** Augmente la poussée d'un pas (+10 % par défaut) ; elle reste bornée entre 0 et 100 % par le mod. */
@action({ UUID: "com.teddyzeus.streamplug.throttle-up" })
export class ThrottleUpAction extends DisplayAction<ThrottleStepSettings> {
	protected override face(context: FaceContext, settings: ThrottleStepSettings): KeyFace {
		return throttleStepFace(context, stepOf(settings));
	}

	override async onKeyDown(ev: KeyDownEvent<ThrottleStepSettings>): Promise<void> {
		await this.command(ev.action, "adjustThrottle", { delta: stepOf(ev.payload.settings) / 100 });
	}
}

/** Diminue la poussée d'un pas (−10 % par défaut). */
@action({ UUID: "com.teddyzeus.streamplug.throttle-down" })
export class ThrottleDownAction extends DisplayAction<ThrottleStepSettings> {
	protected override face(context: FaceContext, settings: ThrottleStepSettings): KeyFace {
		return throttleStepFace(context, -stepOf(settings));
	}

	override async onKeyDown(ev: KeyDownEvent<ThrottleStepSettings>): Promise<void> {
		await this.command(ev.action, "adjustThrottle", { delta: -stepOf(ev.payload.settings) / 100 });
	}
}
