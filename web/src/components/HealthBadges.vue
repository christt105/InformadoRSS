<script setup lang="ts">
import { computed } from 'vue'
import type { Feed } from '../api'
import { dateTime } from '../format'

const props = defineProps<{ feed: Feed }>()

const broken = computed(() => props.feed.routes.flatMap((route) => route.destinations).filter((d) => d.brokenSince !== null).length)
const enabledRoutes = computed(() => props.feed.routes.filter((route) => route.enabled).length)
</script>

<template>
  <span class="row">
    <span
      v-if="feed.health.consecutiveFailures > 0"
      class="badge bad"
      :title="`${feed.health.lastError ?? ''} (${dateTime(feed.health.lastErrorAt)})`"
    >
      Failing ({{ feed.health.consecutiveFailures }}x)
    </span>
    <span v-else-if="feed.health.lastSuccessAt === null" class="badge muted">Not fetched yet</span>
    <span v-else class="badge ok">OK</span>
    <span v-if="broken > 0" class="badge bad">{{ broken }} broken destination{{ broken === 1 ? '' : 's' }}</span>
    <span v-if="enabledRoutes === 0" class="badge warn">No active routes</span>
  </span>
</template>
