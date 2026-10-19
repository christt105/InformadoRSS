<script setup lang="ts">
import { ref } from 'vue'
import { api, ApiRequestError, errorMessage, type Feed, type FeedValidation, type FieldError } from '../api'
import { dateTime } from '../format'
import ErrorList from './ErrorList.vue'

const emit = defineEmits<{ added: [feed: Feed] }>()

const url = ref('')
const name = ref('')
const minutes = ref(10)
const checking = ref(false)
const saving = ref(false)
const preview = ref<FeedValidation | null>(null)
const error = ref<string | null>(null)
const fieldErrors = ref<FieldError[]>([])

function reset() {
  preview.value = null
  error.value = null
  fieldErrors.value = []
}

async function check() {
  reset()
  checking.value = true
  try {
    preview.value = await api.validateFeed(url.value)
    url.value = preview.value.url
    if (!name.value.trim()) {
      name.value = preview.value.title ?? ''
    }
  } catch (e) {
    error.value = errorMessage(e)
    fieldErrors.value = e instanceof ApiRequestError ? (e.body?.errors ?? []) : []
  } finally {
    checking.value = false
  }
}

async function add() {
  saving.value = true
  error.value = null
  try {
    const feed = await api.createFeed({ url: url.value, name: name.value, intervalSeconds: Math.round(minutes.value * 60) })
    emit('added', feed)
    url.value = ''
    name.value = ''
    reset()
  } catch (e) {
    error.value = errorMessage(e)
    fieldErrors.value = e instanceof ApiRequestError ? (e.body?.errors ?? []) : []
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <form class="card stack" @submit.prevent="preview ? add() : check()">
    <h2>Add a feed</h2>
    <label>
      Feed URL
      <input v-model="url" type="url" required placeholder="https://example.com/feed.xml" @input="reset" />
    </label>
    <div v-if="!preview" class="row">
      <button class="primary" type="submit" :disabled="checking || !url.trim()">{{ checking ? 'Checking...' : 'Check' }}</button>
      <span class="muted">The feed is downloaded and parsed before it is added.</span>
    </div>
    <template v-if="preview">
      <div class="notice">
        <strong class="ok">Valid feed</strong>: {{ preview.title ?? 'untitled' }}, {{ preview.itemCount }} item{{ preview.itemCount === 1 ? '' : 's' }}.
        <ul>
          <li v-for="item in preview.items" :key="item.id">
            <a v-if="item.link" :href="item.link" target="_blank" rel="noopener">{{ item.title ?? item.link }}</a>
            <span v-else>{{ item.title ?? item.id }}</span>
            <span v-if="item.published" class="muted"> ({{ dateTime(item.published) }})</span>
          </li>
        </ul>
        <span class="muted">Existing items are not sent: only items published after the first fetch are.</span>
      </div>
      <div class="grid">
        <label>
          Name
          <input v-model="name" type="text" maxlength="200" placeholder="Defaults to the site name" />
        </label>
        <label>
          Check every (minutes)
          <input v-model.number="minutes" type="number" min="1" max="10080" required />
        </label>
      </div>
      <div class="row">
        <button class="primary" type="submit" :disabled="saving">{{ saving ? 'Adding...' : 'Add feed' }}</button>
        <button type="button" @click="reset">Cancel</button>
      </div>
    </template>
    <p v-if="error" class="notice error">{{ error }}</p>
    <ErrorList :errors="fieldErrors" />
  </form>
</template>
