<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { api, errorMessage, type Feed } from '../api'
import AddFeedForm from '../components/AddFeedForm.vue'
import HealthBadges from '../components/HealthBadges.vue'
import { interval, timeAgo } from '../format'

const router = useRouter()
const feeds = ref<Feed[] | null>(null)
const error = ref<string | null>(null)

async function load() {
  try {
    feeds.value = await api.feeds()
    error.value = null
  } catch (e) {
    error.value = errorMessage(e)
  }
}

function added(feed: Feed) {
  router.push({ name: 'new-route', params: { feedId: String(feed.id) } })
}

onMounted(load)
</script>

<template>
  <h1>Feeds</h1>
  <AddFeedForm @added="added" />
  <p v-if="error" class="notice error">{{ error }}</p>
  <p v-else-if="feeds === null" class="muted">Loading...</p>
  <p v-else-if="feeds.length === 0" class="muted">No feeds yet. Paste a feed URL above to add the first one.</p>
  <ul v-else class="plain">
    <li v-for="feed in feeds" :key="feed.id" class="card">
      <div class="row spread">
        <RouterLink :to="{ name: 'feed', params: { feedId: String(feed.id) } }">
          <h2 style="margin: 0">{{ feed.name }}</h2>
        </RouterLink>
        <HealthBadges :feed="feed" />
      </div>
      <div class="muted truncate">{{ feed.url }}</div>
      <div v-if="feed.latestItem" class="truncate">
        Latest:
        <a v-if="feed.latestItem.link" :href="feed.latestItem.link" target="_blank" rel="noopener">
          {{ feed.latestItem.title ?? feed.latestItem.link }}
        </a>
        <span v-else>{{ feed.latestItem.title ?? feed.latestItem.id }}</span>
        <span class="muted after">{{ timeAgo(feed.latestItem.published ?? feed.latestItem.fetchedAt) }}</span>
      </div>
      <div v-if="feed.health.consecutiveFailures > 0 && feed.health.lastError" class="bad">
        {{ feed.health.lastError }} ({{ timeAgo(feed.health.lastErrorAt) }})
      </div>
      <div class="muted">
        Every {{ interval(feed.intervalSeconds) }} · last success {{ timeAgo(feed.health.lastSuccessAt) }} ·
        {{ feed.routes.length }} route{{ feed.routes.length === 1 ? '' : 's' }}
      </div>
    </li>
  </ul>
</template>
