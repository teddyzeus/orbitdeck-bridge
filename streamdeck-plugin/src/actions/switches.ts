import { action, type KeyDownEvent } from "@elgato/streamdeck";

import { rcsFace, sasFace, type FaceContext } from "../rendering/faces.js";
import type { KeyFace } from "../rendering/renderFace.js";
import { DisplayAction } from "./DisplayAction.js";

/** SAS : la touche affiche l'état du jeu ; un appui l'active ou le coupe (`setSas`, idempotent). */
@action({ UUID: "com.teddyzeus.streamplug.sas" })
export class SasAction extends DisplayAction {
	protected override face(context: FaceContext): KeyFace {
		return sasFace(context);
	}

	override async onKeyDown(ev: KeyDownEvent): Promise<void> {
		const enabled = this.store.state?.sas?.enabled;
		if (enabled === undefined) {
			await ev.action.showAlert();
			return;
		}

		await this.command(ev.action, "setSas", { enabled: !enabled });
	}
}

/** RCS : la touche affiche l'état du jeu ; un appui l'active ou le coupe (`setRcs`, idempotent). */
@action({ UUID: "com.teddyzeus.streamplug.rcs" })
export class RcsAction extends DisplayAction {
	protected override face(context: FaceContext): KeyFace {
		return rcsFace(context);
	}

	override async onKeyDown(ev: KeyDownEvent): Promise<void> {
		const enabled = this.store.state?.rcs?.enabled;
		if (enabled === undefined) {
			await ev.action.showAlert();
			return;
		}

		await this.command(ev.action, "setRcs", { enabled: !enabled });
	}
}
