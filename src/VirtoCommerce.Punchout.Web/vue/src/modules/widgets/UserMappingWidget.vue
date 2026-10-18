<template>
  <VcWidget
    icon="lucide-id-card"
    :title="$t('PUNCHOUT.WIDGET.TITLE')"
    :value="mapping ? 1 : 0"
    :disabled="!member?.id"
    @click="open"
  />
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { injectBladeContext, useBlade, useWidgetTrigger } from "@vc-shell/framework";
import { VcWidget } from "@vc-shell/framework/ui";
import { userMappings, type Member, type PunchoutUserMapping } from "../api";

// The contact's punchout user (legacy memberPunchoutUserMappingWidget on customerDetail2), counted like the other
// widgets' badges (0 = not linked; the blade shows the external identity);
// the Customer plugin shares the contact via defineBladeContext({ item })
const ctx = injectBladeContext();
const member = computed(() => ctx.value.item as Member | undefined);
const { openBlade } = useBlade();
const mapping = ref<PunchoutUserMapping>();

async function load() {
  mapping.value = member.value?.id ? await userMappings.byMember(member.value.id).catch(() => undefined) : undefined;
}
watch(() => member.value?.id, load, { immediate: true });
useWidgetTrigger({ onRefresh: load });

const open = () =>
  member.value?.id &&
  openBlade({ name: "PunchoutUserMapping", param: member.value.id, options: { member: member.value }, onClose: load });
</script>
