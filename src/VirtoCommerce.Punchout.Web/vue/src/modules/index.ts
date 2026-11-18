import * as blades from "./pages";
import * as locales from "./locales";
import { defineAppModule, registerExternalWidget, usePermissions } from "@vc-shell/framework";
import { markRaw } from "vue";
import UserMappingWidget from "./widgets/UserMappingWidget.vue";

// The punchout user widget on the contact blade (legacy: widget on customerDetail2, saved contacts, punchout:read)
registerExternalWidget({
  id: "PunchoutUserMapping",
  title: "Punchout user",
  component: markRaw(UserMappingWidget),
  targetBlades: ["CustomerContactDetails"],
  isVisible: (blade) => !!blade?.param && usePermissions().hasAccess("punchout:read"),
});

export default defineAppModule({
  blades,
  locales,
});

export * from "./pages";
