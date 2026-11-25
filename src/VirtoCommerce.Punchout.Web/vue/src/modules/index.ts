import * as blades from "./pages";
import * as locales from "./locales";
import { defineAppModule, registerExternalWidget, usePermissions } from "@vc-shell/framework";
import { markRaw } from "vue";
import UserMappingWidget from "./widgets/UserMappingWidget.vue";
import OrganizationMappingWidget from "./widgets/OrganizationMappingWidget.vue";

// The punchout user widget on the contact blade (legacy: widget on customerDetail2, saved contacts, punchout:read)
registerExternalWidget({
  id: "PunchoutUserMapping",
  title: "Punchout user",
  component: markRaw(UserMappingWidget),
  targetBlades: ["CustomerContactDetails"],
  isVisible: (blade) => !!blade?.param && usePermissions().hasAccess("punchout:read"),
});

// The punchout mapping widget on the organization blade (legacy: widget on organizationDetail2, saved organizations, punchout:read)
registerExternalWidget({
  id: "PunchoutOrganizationMapping",
  title: "Punchout organization",
  component: markRaw(OrganizationMappingWidget),
  targetBlades: ["CustomerOrganizationDetails"],
  isVisible: (blade) => !!blade?.param && usePermissions().hasAccess("punchout:read"),
});

export default defineAppModule({
  blades,
  locales,
});

export * from "./pages";
