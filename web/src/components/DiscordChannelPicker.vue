<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { RouterLink } from 'vue-router'
import { api, ApiRequestError, errorMessage, type Channel, type ChannelPermissions, type Guild, type MessageTemplate } from '../api'
import { loadChannels, loadGuilds } from '../discord'

/** Picks a Discord server and channel, writing `guildId` and `channelId` into `parameters`, and checks the bot's permissions. */
const props = defineProps<{ parameters: Record<string, string>; template: MessageTemplate | null }>()

const guilds = ref<Guild[]>([])
const channels = ref<Channel[]>([])
const error = ref<string | null>(null)
const tokenMissing = ref(false)
const permissions = ref<ChannelPermissions | null>(null)
const testResult = ref<string | null>(null)
const testing = ref(false)

const guildId = computed({
  get: () => props.parameters.guildId ?? '',
  set: (value: string) => {
    props.parameters.guildId = value
    delete props.parameters.channelId
  },
})

const channelId = computed({
  get: () => props.parameters.channelId ?? '',
  set: (value: string) => {
    props.parameters.channelId = value
  },
})

const channel = computed(() => channels.value.find((c) => c.id === channelId.value) ?? null)

const categories = computed(() => {
  const groups = new Map<string, Channel[]>()
  for (const c of channels.value) {
    const name = c.categoryName ?? 'No category'
    groups.set(name, [...(groups.get(name) ?? []), c])
  }
  return [...groups.entries()]
})

const missing = computed(() => permissions.value?.permissions.filter((p) => !p.granted).map((p) => p.label) ?? [])

function fail(e: unknown) {
  error.value = errorMessage(e)
  tokenMissing.value = e instanceof ApiRequestError && (e.body?.error === 'token_missing' || e.body?.error === 'token_invalid')
}

async function refreshGuilds(refresh = false) {
  error.value = null
  try {
    guilds.value = await loadGuilds(refresh)
  } catch (e) {
    fail(e)
  }
}

async function refreshChannels(refresh = false) {
  channels.value = []
  if (!guildId.value) {
    return
  }
  error.value = null
  try {
    channels.value = await loadChannels(guildId.value, refresh)
  } catch (e) {
    fail(e)
  }
}

async function checkPermissions() {
  permissions.value = null
  testResult.value = null
  if (!guildId.value || !channelId.value) {
    return
  }
  try {
    permissions.value = await api.permissions(guildId.value, channelId.value)
  } catch (e) {
    fail(e)
  }
}

async function sendTest() {
  testing.value = true
  testResult.value = null
  try {
    await api.testMessage(channelId.value, props.template)
    testResult.value = 'Test message sent.'
  } catch (e) {
    testResult.value = errorMessage(e)
  } finally {
    testing.value = false
  }
}

async function reload() {
  await refreshGuilds(true)
  await refreshChannels(true)
}

onMounted(async () => {
  await refreshGuilds()
  await refreshChannels()
  await checkPermissions()
})
watch(guildId, () => refreshChannels())
watch(channelId, checkPermissions)
</script>

<template>
  <div class="stack">
    <div class="grid">
      <label>
        Server
        <select v-model="guildId">
          <option value="" disabled>Choose a server</option>
          <option v-if="guildId && !guilds.some((g) => g.id === guildId)" :value="guildId">{{ guildId }}</option>
          <option v-for="guild in guilds" :key="guild.id" :value="guild.id">{{ guild.name }}</option>
        </select>
      </label>
      <label>
        Channel
        <select v-model="channelId" :disabled="!guildId">
          <option value="" disabled>Choose a channel</option>
          <option v-if="channelId && !channel && channels.length === 0" :value="channelId">{{ channelId }}</option>
          <optgroup v-for="[category, list] in categories" :key="category" :label="category">
            <option v-for="c in list" :key="c.id" :value="c.id">
              #{{ c.name }}{{ c.type === 'text' ? '' : ` (${c.type})` }}{{ c.canPost === false ? ' (bot cannot post)' : '' }}
            </option>
          </optgroup>
        </select>
      </label>
    </div>
    <div class="row">
      <button class="small" type="button" @click="reload">Reload servers and channels</button>
      <button class="small" type="button" :disabled="!channelId || testing" @click="sendTest">Send test message</button>
      <span v-if="testResult" class="muted">{{ testResult }}</span>
    </div>
    <p v-if="error" class="notice error">
      {{ error }}
      <RouterLink v-if="tokenMissing" to="/settings">Open settings</RouterLink>
    </p>
    <p v-if="permissions?.timedOutUntil" class="notice">
      The bot is timed out in this server until {{ new Date(permissions.timedOutUntil).toLocaleString() }}.
    </p>
    <p v-if="permissions && !permissions.canPost" class="notice">
      The bot cannot post here. Missing permissions: {{ missing.join(', ') }}.
    </p>
    <p v-else-if="permissions?.canPost" class="ok muted">The bot can post in this channel.</p>
  </div>
</template>
