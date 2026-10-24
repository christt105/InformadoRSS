<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { api, ApiRequestError, errorMessage, type Feed, type FieldError } from '../api'
import ErrorList from '../components/ErrorList.vue'
import FeedItems from '../components/FeedItems.vue'
import HealthBadges from '../components/HealthBadges.vue'
import { dateTime, timeAgo } from '../format'

const props = defineProps<{ feedId: string }>()
const router = useRouter()

const feed = ref<Feed | null>(null)
const url = ref('')
const name = ref('')
const minutes = ref(10)
const identityPattern = ref('')
const error = ref<string | null>(null)
const fieldErrors = ref<FieldError[]>([])
const saved = ref(false)
const saving = ref(false)

function fill(value: Feed) {
  feed.value = value
  url.value = value.url
  name.value = value.name
  minutes.value = value.intervalSeconds / 60
  identityPattern.value = value.identityPattern ?? ''
}

async function load() {
  try {
    fill(await api.feed(Number(props.feedId)))
  } catch (e) {
    error.value = errorMessage(e)
  }
}

async function save() {
  saving.value = true
  saved.value = false
  error.value = null
  fieldErrors.value = []
  try {
    fill(
      await api.updateFeed(Number(props.feedId), {
        url: url.value,
        name: name.value,
        intervalSeconds: Math.round(minutes.value * 60),
        identityPattern: identityPattern.value,
      }),
    )
    saved.value = true
  } catch (e) {
    error.value = errorMessage(e)
    fieldErrors.value = e instanceof ApiRequestError ? (e.body?.errors ?? []) : []
  } finally {
    saving.value = false
  }
}

async function remove() {
  if (!feed.value || !confirm(`Delete "${feed.value.name}" with all its routes?`)) {
    return
  }
  try {
    await api.deleteFeed(feed.value.id)
    router.push({ name: 'feeds' })
  } catch (e) {
    error.value = errorMessage(e)
  }
}

onMounted(load)
watch(() => props.feedId, load)
</script>

<template>
  <p><RouterLink to="/">&larr; Feeds</RouterLink></p>
  <p v-if="error && !feed" class="notice error">{{ error }}</p>
  <template v-if="feed">
    <div class="row spread">
      <h1>{{ feed.name }}</h1>
      <HealthBadges :feed="feed" />
    </div>

    <section class="card stack">
      <h2>Health</h2>
      <div>Last success: {{ timeAgo(feed.health.lastSuccessAt) }} <span class="muted">{{ dateTime(feed.health.lastSuccessAt) }}</span></div>
      <div v-if="feed.health.lastErrorAt">
        Last error: <span :class="feed.health.consecutiveFailures > 0 ? 'bad' : 'muted'">{{ feed.health.lastError }}</span>
        <span class="muted after">{{ timeAgo(feed.health.lastErrorAt) }}</span>
      </div>
      <div v-if="feed.health.consecutiveFailures > 0" class="bad">{{ feed.health.consecutiveFailures }} failed fetches in a row</div>
      <div v-if="feed.nextFetchAt" class="muted">Next check {{ timeAgo(feed.nextFetchAt) }}</div>
      <div v-if="feed.latestItem">
        Latest item:
        <a v-if="feed.latestItem.link" :href="feed.latestItem.link" target="_blank" rel="noopener">{{ feed.latestItem.title ?? feed.latestItem.link }}</a>
        <span v-else>{{ feed.latestItem.title ?? feed.latestItem.id }}</span>
        <span class="muted after">{{ dateTime(feed.latestItem.published) }}</span>
      </div>
    </section>

    <section class="card stack">
      <div class="row spread">
        <h2>Routes</h2>
        <RouterLink :to="{ name: 'new-route', params: { feedId } }"><button class="primary" type="button">Add route</button></RouterLink>
      </div>
      <p v-if="feed.routes.length === 0" class="muted">
        This feed has no routes, so nothing is posted. A route decides which items go where and how they look.
      </p>
      <ul class="plain stack">
        <li v-for="route in feed.routes" :key="route.id">
          <div class="row">
            <RouterLink :to="{ name: 'route', params: { routeId: String(route.id) } }"><strong>{{ route.name }}</strong></RouterLink>
            <span v-if="!route.enabled" class="badge muted">Disabled</span>
            <span v-if="route.filter" class="badge muted">Filtered</span>
            <span v-if="route.template" class="badge muted">Custom template</span>
          </div>
          <ul class="muted">
            <li v-for="(destination, index) in route.destinations" :key="index">
              {{ destination.sink }}:
              {{ destination.parameters.channelId ?? Object.values(destination.parameters).join(', ') }}
              <span v-if="destination.brokenSince" class="bad">broken since {{ dateTime(destination.brokenSince) }}: {{ destination.brokenReason }}</span>
            </li>
          </ul>
        </li>
      </ul>
    </section>

    <FeedItems :feed="feed" />

    <form class="card stack" @submit.prevent="save">
      <h2>Feed settings</h2>
      <label>URL <input v-model="url" type="url" required /></label>
      <div class="grid">
        <label>Name <input v-model="name" type="text" maxlength="200" /></label>
        <label>Check every (minutes) <input v-model.number="minutes" type="number" min="1" max="10080" required /></label>
      </div>
      <label>
        Identity pattern <input v-model="identityPattern" type="text" maxlength="500" placeholder="optional regular expression" />
        <span class="muted">
          For feeds that rewrite an entry instead of adding a new one. The part of the description it matches becomes part of
          the item id, so a change there is sent as a new item. Changing it marks the current items as seen without sending them.
        </span>
      </label>
      <div class="row">
        <button class="primary" type="submit" :disabled="saving">Save</button>
        <span v-if="saved" class="ok">Saved</span>
        <span style="flex: 1"></span>
        <button class="danger" type="button" @click="remove">Delete feed</button>
      </div>
      <p v-if="error" class="notice error">{{ error }}</p>
      <ErrorList :errors="fieldErrors" />
    </form>
  </template>
</template>
