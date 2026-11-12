using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OrbitDeck.Mod.Telemetry;

namespace OrbitDeck.Mod.Protocol
{
    /// <summary>Codes d'erreur du protocole (voir protocol/README.md).</summary>
    public static class ErrorCodes
    {
        public const string BadRequest = "BAD_REQUEST";
        public const string UnknownCommand = "UNKNOWN_COMMAND";
        public const string NoVessel = "NO_VESSEL";
        public const string NotControllable = "NOT_CONTROLLABLE";
        public const string RejectedByGame = "REJECTED_BY_GAME";
        public const string Internal = "INTERNAL";
    }

    /// <summary>Construction des messages serveur → client (protocole v1). Ne dépend d'aucun type du jeu.</summary>
    public static class ProtocolMessages
    {
        public const int Version = 1;

        public static string Hello(string modVersion, string gameVersion) =>
            Serialize(Base("hello", new JObject
            {
                ["mod"] = modVersion ?? string.Empty,
                ["game"] = gameVersion ?? string.Empty
            }));

        public static string State(TelemetrySnapshot snapshot, long sequence)
        {
            var message = Base("state", new JObject
            {
                ["seq"] = sequence,
                ["status"] = StatusName(snapshot.Status),
                ["gameState"] = snapshot.GameStateName
            });

            if (snapshot.Status != VesselStatus.NoVessel)
            {
                message["vessel"] = new JObject { ["name"] = snapshot.VesselName };
                message["altitude"] = new JObject
                {
                    ["seaLevel"] = Number(snapshot.AltitudeSeaLevel, 2),
                    ["surface"] = Number(snapshot.AltitudeSurface, 2)
                };
                message["speed"] = new JObject
                {
                    ["mode"] = SpeedModeName(snapshot.SpeedMode),
                    ["surface"] = Number(snapshot.SpeedSurface, 2),
                    ["orbital"] = Number(snapshot.SpeedOrbital, 2),
                    ["target"] = snapshot.SpeedTarget.HasValue ? Number(snapshot.SpeedTarget.Value, 2) : JValue.CreateNull()
                };
                message["throttle"] = Math.Round(Math.Min(1f, Math.Max(0f, snapshot.Throttle)), 3);
                var sas = new JObject
                {
                    ["enabled"] = snapshot.SasEnabled,
                    ["mode"] = snapshot.SasMode
                };
                if (snapshot.AvailableSasModes != null)
                {
                    var available = new JArray();
                    foreach (var mode in snapshot.AvailableSasModes)
                        available.Add(SasModeName(mode));
                    sas["available"] = available;
                }

                message["sas"] = sas;
                message["rcs"] = new JObject { ["enabled"] = snapshot.RcsEnabled };

                // Champs ajoutés après la v1 initiale : absents quand le jeu ne les fournit pas.
                if (snapshot.LightsEnabled.HasValue)
                    message["lights"] = new JObject { ["enabled"] = snapshot.LightsEnabled.Value };
                if (snapshot.BrakesEnabled.HasValue)
                    message["brakes"] = new JObject { ["enabled"] = snapshot.BrakesEnabled.Value };
                if (snapshot.GearEnabled.HasValue)
                    message["gear"] = new JObject { ["enabled"] = snapshot.GearEnabled.Value };
                if (snapshot.View.HasValue)
                    message["view"] = snapshot.View.Value == ViewKind.Map ? "map" : "ship";
                if (snapshot.StageCount.HasValue)
                {
                    var stage = new JObject { ["count"] = snapshot.StageCount.Value };
                    if (snapshot.StageExhausted.HasValue)
                        stage["exhausted"] = snapshot.StageExhausted.Value;
                    message["stage"] = stage;
                }

                if (snapshot.Time != null)
                    message["time"] = TimeObject(snapshot.Time);
                if (snapshot.Maneuver != null)
                    message["maneuver"] = ManeuverObject(snapshot.Maneuver);

                // 2.0 : ajouts après « maneuver », chacun seulement s'il est lisible (un état sans ces blocs est identique à celui du mod 0.5).
                if (snapshot.Orbit != null)
                    message["orbit"] = CurrentOrbitObject(snapshot.Orbit);
                if (snapshot.Resources != null)
                    message["resources"] = ResourcesObject(snapshot.Resources);
                if (snapshot.Performance != null)
                    message["performance"] = PerformanceObject(snapshot.Performance);
                if (snapshot.Landing != null)
                    message["landing"] = LandingObject(snapshot.Landing);
                if (snapshot.Target != null)
                    message["target"] = TargetObject(snapshot.Target);
                if (snapshot.Systems != null)
                    message["systems"] = SystemsObject(snapshot.Systems);
                if (snapshot.Docking != null)
                    message["docking"] = DockingObject(snapshot.Docking);
                if (snapshot.ActionGroups != null && snapshot.ActionGroups.Custom != null)
                    message["actionGroups"] = ActionGroupsObject(snapshot.ActionGroups);
            }

            return Serialize(message);
        }

        /// <summary>Bonjour avec les familles que le mod sait lire ou commander (2.0). Sans famille, le message est celui de la 1.1.</summary>
        public static string Hello(string modVersion, string gameVersion, System.Collections.Generic.IEnumerable<string> features)
        {
            var body = new JObject
            {
                ["mod"] = modVersion ?? string.Empty,
                ["game"] = gameVersion ?? string.Empty
            };

            if (features != null)
            {
                var list = new JArray();
                foreach (var feature in features)
                    list.Add(feature);
                if (list.Count > 0)
                    body["features"] = list;
            }

            return Serialize(Base("hello", body));
        }

        private static void Put(JObject target, string name, double? value, int decimals)
        {
            if (value.HasValue)
                target[name] = Number(value.Value, decimals);
        }

        private static JObject CurrentOrbitObject(CurrentOrbitInfo orbit)
        {
            var body = new JObject();
            Put(body, "ap", orbit.Ap, 0);
            Put(body, "pe", orbit.Pe, 0);
            Put(body, "period", orbit.Period, 1);
            Put(body, "inc", orbit.Inc, 2);
            Put(body, "apUt", orbit.ApUt, 1);
            Put(body, "peUt", orbit.PeUt, 1);
            if (orbit.Body != null)
                body["body"] = orbit.Body;
            if (orbit.Escape)
                body["escape"] = true;
            Put(body, "atmosphere", orbit.Atmosphere, 0);
            return body;
        }

        private static JObject ResourceSetObject(ResourceSet set, JObject body)
        {
            void One(string name, ResourceAmount amount)
            {
                if (amount != null)
                    body[name] = new JObject { ["amount"] = Number(amount.Amount, 1), ["max"] = Number(amount.Max, 1) };
            }

            One("fuel", set.Fuel);
            One("oxidizer", set.Oxidizer);
            One("electric", set.Electric);
            One("monoprop", set.Monoprop);
            return body;
        }

        private static JObject ResourcesObject(ResourcesInfo resources)
        {
            var body = new JObject();
            if (resources.Vessel != null)
                ResourceSetObject(resources.Vessel, body);
            if (resources.Stage != null)
                body["stage"] = ResourceSetObject(resources.Stage, new JObject());
            return body;
        }

        private static JObject PerformanceObject(PerformanceInfo performance)
        {
            var body = new JObject();
            Put(body, "dv", performance.Dv, 1);
            Put(body, "dvStage", performance.DvStage, 1);
            Put(body, "twr", performance.Twr, 2);
            return body;
        }

        private static JObject LandingObject(LandingInfo landing)
        {
            var body = new JObject();
            Put(body, "vspeed", landing.VSpeed, 2);
            Put(body, "hspeed", landing.HSpeed, 2);
            Put(body, "radar", landing.Radar, 1);
            Put(body, "impactUt", landing.ImpactUt, 1);
            return body;
        }

        private static JObject TargetObject(TargetInfo target)
        {
            var body = new JObject();
            if (target.None)
                body["none"] = true;
            if (target.Name != null)
                body["name"] = target.Name;
            Put(body, "distance", target.Distance, 1);
            Put(body, "rel", target.Rel, 2);
            return body;
        }

        private static string SystemStateName(SystemState state)
        {
            switch (state)
            {
                case SystemState.Extended: return "extended";
                case SystemState.Retracted: return "retracted";
                case SystemState.Mixed: return "mixed";
                case SystemState.Moving: return "moving";
                case SystemState.Ready: return "ready";
                case SystemState.Deployed: return "deployed";
                default: return "cut";
            }
        }

        private static JObject SystemCategoryObject(SystemCategory category)
        {
            var body = new JObject { ["count"] = category.Count };
            if (category.State.HasValue)
                body["state"] = SystemStateName(category.State.Value);
            if (category.Retractable == false)
                body["retractable"] = false;
            return body;
        }

        private static JObject SystemsObject(SystemsInfo systems)
        {
            var body = new JObject();
            if (systems.Solar != null)
                body["solar"] = SystemCategoryObject(systems.Solar);
            if (systems.Antennas != null)
                body["antennas"] = SystemCategoryObject(systems.Antennas);
            if (systems.Ladders != null)
                body["ladders"] = SystemCategoryObject(systems.Ladders);
            if (systems.Parachutes != null)
                body["parachutes"] = SystemCategoryObject(systems.Parachutes);
            return body;
        }

        private static JObject ActionGroupsObject(ActionGroupsInfo groups)
        {
            var custom = new JArray();
            foreach (var state in groups.Custom)
                custom.Add(state.ToString().ToLowerInvariant());
            return new JObject { ["custom"] = custom };
        }

        private static JObject DockingObject(DockingInfo docking)
        {
            var body = new JObject { ["ports"] = docking.Ports };
            if (docking.State.HasValue)
                body["state"] = docking.State.Value.ToString().ToLowerInvariant();
            if (docking.Armable.HasValue)
                body["armable"] = docking.Armable.Value;
            if (docking.CanUndock.HasValue)
                body["canUndock"] = docking.CanUndock.Value;
            return body;
        }

        /// <summary>Objet « time » de l'état : paliers, pause et instant de jeu. Les champs inconnus sont omis, jamais écrits à 0.</summary>
        private static JObject TimeObject(TimeInfo time)
        {
            var rates = new JArray();
            foreach (var rate in time.Rates)
                rates.Add(Math.Round(rate, 3));

            var body = new JObject
            {
                ["ut"] = Number(time.Ut, 1),
                ["rate"] = Number(time.Rate, 3),
                ["index"] = time.Index,
                ["rates"] = rates,
                ["mode"] = time.Mode == WarpMode.Physics ? "physics" : "rails",
                ["paused"] = time.Paused
            };

            if (time.Pausable)
                body["pausable"] = true;
            if (time.MaxIndex.HasValue)
                body["maxIndex"] = time.MaxIndex.Value;
            if (time.Limit.HasValue)
                body["limit"] = WarpLimitName(time.Limit.Value);
            if (time.Auto)
                body["auto"] = true;
            if (time.DayLength.HasValue)
                body["day"] = Number(time.DayLength.Value, 0);

            return body;
        }

        /// <summary>Objet « maneuver » de l'état : nœuds (huit au plus), orbite actuelle, delta-V et combustion. Champs inconnus omis.</summary>
        private static JObject ManeuverObject(ManeuverInfo maneuver)
        {
            var nodes = new JArray();
            foreach (var node in maneuver.Nodes)
            {
                if (nodes.Count >= ManeuverInfo.MaxNodes)
                    break;

                var item = new JObject { ["ut"] = Number(node.Ut, 1), ["dv"] = Number(node.Dv, 1) };
                if (node.Burn.HasValue)
                    item["burn"] = Number(node.Burn.Value, 1);
                if (node.Ignition.HasValue)
                    item["ignition"] = Number(node.Ignition.Value, 1);
                if (node.After != null)
                    item["after"] = OrbitObject(node.After);
                nodes.Add(item);
            }

            var body = new JObject { ["count"] = maneuver.Count, ["nodes"] = nodes };
            if (maneuver.Before != null)
                body["before"] = OrbitObject(maneuver.Before);
            if (maneuver.DvAvailable.HasValue)
                body["dvAvailable"] = Number(maneuver.DvAvailable.Value, 1);
            if (maneuver.Burning != null)
            {
                var burning = new JObject();
                if (maneuver.Burning.Remaining.HasValue)
                    burning["remaining"] = Number(maneuver.Burning.Remaining.Value, 1);
                if (maneuver.Burning.Progress.HasValue)
                    burning["progress"] = Number(maneuver.Burning.Progress.Value, 3);
                body["burning"] = burning;
            }

            if (maneuver.Warp != null)
            {
                var warp = new JObject { ["state"] = WarpStateName(maneuver.Warp.State) };
                if (maneuver.Warp.Stop.HasValue)
                    warp["stop"] = Number(maneuver.Warp.Stop.Value, 1);
                if (maneuver.Warp.Reason.HasValue)
                    warp["reason"] = WarpReasonName(maneuver.Warp.Reason.Value);
                body["warp"] = warp;
            }

            return body;
        }

        private static string WarpStateName(ManeuverWarpState state)
        {
            switch (state)
            {
                case ManeuverWarpState.Running: return "running";
                case ManeuverWarpState.Arrived: return "arrived";
                case ManeuverWarpState.Late: return "late";
                case ManeuverWarpState.Interrupted: return "interrupted";
                default: return "idle";
            }
        }

        private static string WarpReasonName(ManeuverWarpReason reason)
        {
            switch (reason)
            {
                case ManeuverWarpReason.NodeGone: return "nodeGone";
                case ManeuverWarpReason.Vessel: return "vessel";
                case ManeuverWarpReason.Control: return "control";
                default: return "manual";
            }
        }

        private static JObject OrbitObject(OrbitInfo orbit)
        {
            var body = new JObject();
            if (orbit.Ap.HasValue)
                body["ap"] = Number(orbit.Ap.Value, 0);
            if (orbit.Pe.HasValue)
                body["pe"] = Number(orbit.Pe.Value, 0);
            if (orbit.Escape)
                body["escape"] = true;
            if (orbit.Atmosphere.HasValue)
                body["atmosphere"] = Number(orbit.Atmosphere.Value, 0);
            return body;
        }

        public static string WarpLimitName(WarpLimit limit)
        {
            switch (limit)
            {
                case WarpLimit.Atmosphere: return "atmosphere";
                case WarpLimit.Altitude: return "altitude";
                case WarpLimit.Thrust: return "thrust";
                case WarpLimit.Ground: return "ground";
                case WarpLimit.Proximity: return "proximity";
                default: return "other";
            }
        }

        public static string Ack(string id) => Serialize(Base("ack", new JObject { ["id"] = id }));

        public static string Error(string id, string code, string message)
        {
            var body = new JObject { ["code"] = code, ["message"] = message ?? string.Empty };
            if (id != null)
                body["id"] = id;
            return Serialize(Base("error", body));
        }

        public static string Pong() => Serialize(Base("pong", new JObject()));

        /// <summary>Nom d'un mode du SAS dans le protocole (le même que celui de la commande « setSasMode »).</summary>
        public static string SasModeName(SasModeKind mode)
        {
            switch (mode)
            {
                case SasModeKind.Prograde: return "prograde";
                case SasModeKind.Retrograde: return "retrograde";
                case SasModeKind.Normal: return "normal";
                case SasModeKind.Antinormal: return "antinormal";
                case SasModeKind.RadialIn: return "radialIn";
                case SasModeKind.RadialOut: return "radialOut";
                case SasModeKind.Target: return "target";
                case SasModeKind.AntiTarget: return "antiTarget";
                case SasModeKind.Maneuver: return "maneuver";
                default: return "stabilityAssist";
            }
        }

        public static string StatusName(VesselStatus status)
        {
            switch (status)
            {
                case VesselStatus.Connected: return "connected";
                case VesselStatus.NotControllable: return "notControllable";
                default: return "noVessel";
            }
        }

        public static string SpeedModeName(SpeedMode mode)
        {
            switch (mode)
            {
                case SpeedMode.Orbit: return "orbit";
                case SpeedMode.Target: return "target";
                default: return "surface";
            }
        }

        private static JObject Base(string type, JObject body)
        {
            var message = new JObject { ["v"] = Version, ["type"] = type };
            foreach (var property in body.Properties())
                message[property.Name] = property.Value;
            return message;
        }

        // NaN et l'infini ne sont pas du JSON valide : ils deviennent null.
        private static JToken Number(double value, int decimals) =>
            double.IsNaN(value) || double.IsInfinity(value) ? JValue.CreateNull() : new JValue(Math.Round(value, decimals));

        private static string Serialize(JObject message) => message.ToString(Formatting.None);
    }
}
