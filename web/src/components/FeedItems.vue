<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { api, errorMessage, type Feed, type FeedItemStatus, type FeedItems } from '../api'
import { dateTime, timeAgo } from '../format'
import DiscordChannelPicker from './DiscordChannelPicker.vue'

/** Lists the items the feed has right now and lets the user send one by hand or make routes forget it. */
const props = defineProps<{ feed: Feed }>()

const data = ref<FeedItems | null>(null)
const loading = ref(false)
const error = ref<string | null>(null)
const busy = ref<string | null>(null)
const messages = reactive<Record<string, { ok: boolean; text: string }>>({})
const routeId = ref<number | null>(null)
const channel = reactive<Record<string, string>>({})

const routes = computed(() => props.feed.routes)
const maxAgeDays = computed(() => (data.value ? Math.round(data.value.maxItemAgeSeconds / 86400) : 0))

watch(
  routes,
  (list) => {
    if (!list.some((route) => route.id === routeId.value)) {
      routeId.value = list[0]?.id ?? null
    }
  },
  { immediate: true },
)

async function load() {
  loading.value = true
  error.value = null
  try {
    data.value = await api.feedItems(props.feed.id)
  } catch (e) {
    error.value = errorMessage(e)
  } finally {
    loading.value = false
  }
}

function say(item: FeedItemStatus, ok: boolean, text: string) {
  messages[item.id] = { ok, text }
}

async function send(item: FeedItemStatus) {
  if (routeId.value === null) {
    return
  }
  const target = channel.channelId ? `channel ${channel.channelId}` : 'the route destinations'
  if (!confirm(`Send "${item.title ?? item.id}" to ${target}?`)) {
    return
  }
  busy.value = item.id
  try {
    const result = await api.sendItem(props.feed.id, item.id, routeId.value, channel.channelId || undefined)
    say(item, result.sent, result.sent ? 'Sent' : result.errors.join('; '))
  } catch (e) {
    say(item, false, errorMessage(e))
  } finally {
    busy.value = null
  }
}

async function reset(item: FeedItemStatus) {
  const note = item.tooOld ? ' It is older than the maximum item age, so it will be skipped again.' : ''
  if (!confirm(`Make every route forget "${item.title ?? item.id}"? The next check treats it as new.${note}`)) {
    return
  }
  busy.value = item.id
  try {
    const result = await api.resetItem(props.feed.id, item.id)
    say(item, true, result.reset === 0 ? 'No route had seen it' : 'Reset, it is sent again on the next check')
    await load()
  } catch (e) {
    say(item, false, errorMessage(e))
  } finally {
    busy.value = null
  }
}
</script>

<template>
  <section class="card stack">
    <div class="row spread">
      <h2>Items</h2>
      <button type="button" :disabled="loading" @click="load">{{ data ? 'Refresh' : 'Load items' }}</button>
    </div>
    <p class="muted">
      Fetches the feed now. Send posts one item right away without changing what routes consider seen. Reset makes routes
      forget an item so the next check treats it as new.
    </p>
    <p v-if="error" class="notice error">{{ error }}</p>
    <template v-if="data">
      <p class="muted">Items published more than {{ maxAgeDays }} days ago are skipped automatically.</p>
      <template v-if="routes.length > 0">
        <label>
          Message style of route
          <select v-model="routeId">
            <option v-for="route in routes" :key="route.id" :value="route.id">{{ route.name }}</option>
          </select>
        </label>
        <details>
          <summary>Send to a specific channel{{ channel.channelId ? ` (${channel.channelId})` : ' (default: route destinations)' }}</summary>
          <DiscordChannelPicker :parameters="channel" :template="null" />
        </details>
      </template>
      <p v-else class="muted">Add a route to send items by hand.</p>
      <p v-if="data.items.length === 0" class="muted">The feed has no items.</p>
      <ul class="plain stack">
        <li v-for="item in data.items" :key="item.id" class="stack">
          <div class="row">
            <a v-if="item.link" :href="item.link" target="_blank" rel="noopener"><strong>{{ item.title ?? item.link }}</strong></a>
            <strong v-else>{{ item.title ?? item.id }}</strong>
            <span class="muted">{{ item.published ? `${timeAgo(item.published)}, ${dateTime(item.published)}` : 'no date' }}</span>
            <span v-if="item.tooOld" class="badge warn">Too old</span>
          </div>
          <div class="row">
            <span v-for="state in item.routes" :key="state.routeId" class="badge" :class="state.seen ? 'muted' : 'ok'">
              {{ state.routeName }}: {{ state.seen ? 'seen' : 'pending' }}
            </span>
          </div>
          <div class="row">
            <button type="button" :disabled="busy !== null || routeId === null" @click="send(item)">Send now</button>
            <button type="button" :disabled="busy !== null || !item.routes.some((state) => state.seen)" @click="reset(item)">Reset</button>
            <span v-if="messages[item.id]" :class="messages[item.id]!.ok ? 'ok' : 'bad'">{{ messages[item.id]!.text }}</span>
          </div>
        </li>
      </ul>
    </template>
  </section>
</template>
