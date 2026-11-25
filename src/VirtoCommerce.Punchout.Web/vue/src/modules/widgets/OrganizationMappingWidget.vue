<template>
  <VcWidget
    icon="lucide-id-card"
    :title="$t('PUNCHOUT.WIDGET.ORGANIZATION_TITLE')"
    :value="mapping ? 1 : 0"
    :disabled="!organization?.id"
    @click="open"
  />
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { injectBladeContext, useBlade, useWidgetTrigger } from "@vc-shell/framework";
import { VcWidget } from "@vc-shell/framework/ui";
import { userMappings, type Member, type PunchoutUserMapping } from "../api";

// The organization's punchout mapping (legacy memberPunchoutUserMappingWidget on organizationDetail2);
// the Customer plugin shares the organization via defineBladeContext({ item })
const ctx = injectBladeContext();
const organization = computed(() => ctx.value.item as Member | undefined);
const { openBlade } = useBlade();
const mapping = ref<PunchoutUserMapping>();

async function load() {
  mapping.value = organization.value?.id
    ? await userMappings.byOrganization(organization.value.id).catch(() => undefined)
    : undefined;
}
watch(() => organization.value?.id, load, { immediate: true });
useWidgetTrigger({ onRefresh: load });

const open = () =>
  organization.value?.id &&
  openBlade({
    name: "PunchoutOrganizationMapping",
    param: organization.value.id,
    options: { organization: organization.value },
    onClose: load,
  });
</script>
