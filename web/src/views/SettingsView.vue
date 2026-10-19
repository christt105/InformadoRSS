<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { api, ApiRequestError, errorMessage, type FieldError, type Setting } from '../api'
import ErrorList from '../components/ErrorList.vue'

const settings = ref<Setting[] | null>(null)
const drafts = reactive<Record<string, string>>({})
const error = ref<string | null>(null)
const fieldErrors = ref<FieldError[]>([])
const saved = ref(false)
const saving = ref(false)
const newKey = ref('')
const newValue = ref('')

const groups = computed(() => {
  const map = new Map<string, Setting[]>()
  for (const setting of settings.value ?? []) {
    map.set(setting.group, [...(map.get(setting.group) ?? []), setting])
  }
  return [...map.entries()]
})

const changes = computed(() => {
  const result: Record<string, string | null> = {}
  for (const setting of settings.value ?? []) {
    const draft = drafts[setting.key] ?? ''
    if (setting.secret ? draft !== '' : draft !== (setting.value ?? '')) {
      result[setting.key] = draft === '' ? null : draft
    }
  }
  if (newKey.value.trim()) {
    result[newKey.value.trim()] = newValue.value
  }
  return result
})

function fill(list: Setting[]) {
  settings.value = list
  for (const key of Object.keys(drafts)) {
    delete drafts[key]
  }
  for (const setting of list) {
    drafts[setting.key] = setting.secret ? '' : (setting.value ?? '')
  }
}

async function apply(change: Record<string, string | null>) {
  saving.value = true
  saved.value = false
  error.value = null
  fieldErrors.value = []
  try {
    fill(await api.updateSettings(change))
    newKey.value = ''
    newValue.value = ''
    saved.value = true
  } catch (e) {
    error.value = errorMessage(e)
    fieldErrors.value = e instanceof ApiRequestError ? (e.body?.errors ?? []) : []
  } finally {
    saving.value = false
  }
}

function clear(setting: Setting) {
  if (confirm(`Remove the value of "${setting.label}"?`)) {
    apply({ [setting.key]: null })
  }
}

onMounted(async () => {
  try {
    fill(await api.settings())
  } catch (e) {
    error.value = errorMessage(e)
  }
})
</script>

<template>
  <h1>Settings</h1>
  <p v-if="settings === null && !error" class="muted">Loading...</p>
  <form v-if="settings" class="stack" @submit.prevent="apply(changes)">
    <section v-for="[group, list] in groups" :key="group" class="card stack">
      <h2>{{ group }}</h2>
      <div v-for="setting in list" :key="setting.key">
        <label>
          {{ setting.label }}
          <span v-if="setting.secret" class="badge" :class="setting.isSet ? 'ok' : 'muted'">{{ setting.isSet ? 'Set' : 'Not set' }}</span>
          <span class="row">
            <input
              v-model="drafts[setting.key]"
              :type="setting.secret ? 'password' : 'text'"
              :placeholder="setting.secret && setting.isSet ? 'Hidden; type a new value to replace it' : (setting.placeholder ?? '')"
              autocomplete="off"
              style="flex: 1; width: auto"
            />
            <button v-if="setting.isSet" class="small danger" type="button" @click="clear(setting)">Clear</button>
          </span>
        </label>
        <div class="muted">
          {{ setting.description }}
          <code v-if="!setting.defined">{{ setting.key }}</code>
        </div>
      </div>
    </section>

    <details class="card">
      <summary>Add another setting by key</summary>
      <div class="grid" style="margin-top: 0.75rem">
        <label>Key <input v-model="newKey" type="text" placeholder="e.g. alerts.discordChannelId" /></label>
        <label>Value <input v-model="newValue" type="text" /></label>
      </div>
    </details>

    <div class="row">
      <button class="primary" type="submit" :disabled="saving || Object.keys(changes).length === 0">Save</button>
      <span v-if="saved" class="ok">Saved</span>
    </div>
  </form>
  <p v-if="error" class="notice error">{{ error }}</p>
  <ErrorList :errors="fieldErrors" />
</template>
