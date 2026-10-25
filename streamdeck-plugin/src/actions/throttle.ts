import { action } from "@elgato/streamdeck";

import { throttleFace, type FaceContext } from "../rendering/faces.js";
import type { KeyFace } from "../rendering/renderFace.js";
import { DisplayAction } from "./DisplayAction.js";

/** Affiche la poussée courante du véhicule (pourcentage et jauge). */
@action({ UUID: "com.teddyzeus.streamplug.throttle" })
export class ThrottleAction extends DisplayAction {
	protected override face(context: FaceContext): KeyFace {
		return throttleFace(context);
	}
}
