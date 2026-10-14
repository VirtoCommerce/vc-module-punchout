<template>
  <VcBlade
    :title="$t('PUNCHOUT.MAPPING.TITLE')"
    :subtitle="member?.name"
    :toolbar-items="toolbar"
    :loading="loading"
    width="40%"
  >
    <VcContainer>
      <VcForm class="punchout-form">
        <VcBanner
          v-if="member && !accounts.length"
          variant="warning"
        >
          {{ $t("PUNCHOUT.MAPPING.NO_ACCOUNTS") }}
        </VcBanner>
        <!-- rendered once loaded, so the required field isn't flagged before the user types -->
        <VcCard
          v-if="item.memberId"
          :header="$t('PUNCHOUT.MAPPING.CARD')"
        >
          <div class="punchout-card">
            <VcSwitch
              v-model="item.isActive"
              :label="$t('PUNCHOUT.MAPPING.IS_ACTIVE')"
            />
            <Field
              v-slot="{ errorMessage, handleChange, errors }"
              name="externalId"
              rules="required"
              :label="$t('PUNCHOUT.MAPPING.EXTERNAL_ID')"
              :model-value="item.externalId"
            >
              <VcInput
                v-model="item.externalId"
                :label="$t('PUNCHOUT.MAPPING.EXTERNAL_ID')"
                :placeholder="$t('PUNCHOUT.MAPPING.EXTERNAL_ID_PLACEHOLDER')"
                :hint="$t('PUNCHOUT.MAPPING.EXTERNAL_ID_HINT')"
                required
                :error="!!errors.length"
                :error-message="errorMessage"
                @update:model-value="handleChange"
              />
            </Field>
            <!-- legacy picked the first account; the choice matters only for a contact with several -->
            <VcSelect
              v-if="accounts.length > 1"
              :model-value="item.userId"
              :label="$t('PUNCHOUT.MAPPING.ACCOUNT')"
              :options="accounts"
              option-value="id"
              option-label="userName"
              :clearable="false"
              @update:model-value="(id) => setUser(id as string)"
            />
            <VcField
              v-else-if="item.userName"
              :label="$t('PUNCHOUT.MAPPING.ACCOUNT')"
              :model-value="item.userName"
            />
          </div>
        </VcCard>
      </VcForm>
    </VcContainer>
  </VcBlade>
</template>

<script setup lang="ts">
import { useBlade, useBladeForm, usePermissions, usePopup, type IBladeToolbar } from "@vc-shell/framework";
import { VcBanner, VcBlade, VcCard, VcContainer, VcField, VcForm, VcInput, VcSelect, VcSwitch } from "@vc-shell/framework/ui";
import { Field } from "vee-validate";
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { ApiError, getMember, userMappings, type Member, type PunchoutUserMapping } from "../api";

// The punchout user of a contact (param: contact id); legacy userMappingDetailController
defineBlade({ url: "/punchout-user-mapping", name: "PunchoutUserMapping", permissions: ["punchout:read"] });

const { t } = useI18n({ useScope: "global" });
const { param, options, closeSelf } = useBlade<{ member?: Member }>();
const { showConfirmation, showError } = usePopup();
const { hasAccess } = usePermissions();

const member = ref<Member | undefined>(options.value?.member);
const accounts = computed(() => member.value?.securityAccounts ?? []);
const item = ref<PunchoutUserMapping>({});
const savedExternalId = ref<string>();
const loading = ref(false);
const isNew = computed(() => !item.value.id);
const form = useBladeForm({ data: item, closeConfirmMessage: computed(() => t("PUNCHOUT.ALERTS.CLOSE_CONFIRMATION")) });

function setUser(userId?: string) {
  item.value.userId = userId;
  item.value.userName = accounts.value.find((a) => a.id === userId)?.userName;
}

// server validation answers { errorCode, errorParameters }
function report(e: unknown) {
  const body = e instanceof ApiError ? (e.body as { errorCode?: string; errorParameters?: Record<string, unknown> }) : undefined;
  showError(body?.errorCode ? t(`PUNCHOUT.ERRORS.${body.errorCode}`, body.errorParameters ?? {}) : String((e as Error)?.message ?? e));
}

async function busy(action: () => Promise<unknown>) {
  loading.value = true;
  try {
    await action();
  } catch (e) {
    report(e);
  } finally {
    loading.value = false;
  }
}

const load = () =>
  busy(async () => {
    // options don't survive a page reload
    member.value ??= await getMember(param.value!);
    const found = await userMappings.byMember(param.value!);
    item.value = found ?? { ...(await userMappings.getNew()), memberId: param.value };
    if (!found && accounts.value.length) setUser(accounts.value[0].id);
    savedExternalId.value = found?.externalId;
    form.setBaseline();
  });

const save = () =>
  busy(async () => {
    item.value = await (isNew.value ? userMappings.create(item.value) : userMappings.update(item.value));
    form.setBaseline();
    await closeSelf();
  });

const toolbar = ref<IBladeToolbar[]>([
  {
    id: "save",
    title: t("PUNCHOUT.TOOLBAR.SAVE"),
    icon: "lucide-save",
    permissions: "punchout:update",
    disabled: computed(
      () => !form.canSave.value || !accounts.value.length || (isNew.value && !hasAccess("punchout:create")),
    ),
    clickHandler: () => save(),
  },
  { id: "refresh", title: t("PUNCHOUT.TOOLBAR.REFRESH"), icon: "lucide-refresh-cw", clickHandler: () => load() },
  {
    id: "reset",
    title: t("PUNCHOUT.TOOLBAR.RESET"),
    icon: "lucide-undo-2",
    permissions: "punchout:update",
    disabled: computed(() => !form.isModified.value),
    clickHandler: () => form.revert(),
  },
  {
    id: "delete",
    title: t("PUNCHOUT.TOOLBAR.DELETE"),
    icon: "lucide-trash-2",
    permissions: "punchout:delete",
    disabled: computed(() => isNew.value),
    async clickHandler() {
      if (!(await showConfirmation(t("PUNCHOUT.ALERTS.DELETE", { externalId: savedExternalId.value })))) return;
      await busy(async () => {
        await userMappings.delete(item.value.id!);
        form.setBaseline();
        await closeSelf();
      });
    },
  },
]);

onMounted(load);
</script>

<style lang="scss">
.punchout-form {
  padding: 20px 22px 36px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.punchout-card {
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}
</style>
