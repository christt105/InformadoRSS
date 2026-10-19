<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import type { FieldError, MessageTemplate } from '../api'
import ErrorList from './ErrorList.vue'

/**
 * Edits a route template: message text plus an optional embed. Emits `null` while "default template" is chosen.
 * Remount it (with a new `key`) to load a different template.
 */
const props = defineProps<{
  modelValue: MessageTemplate | null
  errors: FieldError[]
  placeholders: string[]
  defaultContent: string
}>()
const emit = defineEmits<{ 'update:modelValue': [value: MessageTemplate | null] }>()

const domainToken = '{{category:<domain>}}'
const typeToken = '{{category:type}}'

type TextField ='content' | 'title' | 'description' | 'url' | 'image' | 'thumbnail' | 'footer' | 'author'

const initial = props.modelValue
const useDefault = ref(initial === null)
const useEmbed = ref(initial?.embed !== undefined && initial?.embed !== null)
const useColor = ref(initial?.embed?.color !== undefined)
const color = ref(`#${(initial?.embed?.color ?? 0x5865f2).toString(16).padStart(6, '0')}`)
const timestamp = ref(initial?.embed?.timestamp ?? false)
const text = reactive<Record<TextField, string>>({
  content: initial?.content ?? (initial === null ? props.defaultContent : ''),
  title: initial?.embed?.title ?? '{{title}}',
  description: initial?.embed?.description ?? '',
  url: initial?.embed?.url ?? '{{link}}',
  image: initial?.embed?.image ?? '',
  thumbnail: initial?.embed?.thumbnail ?? '',
  footer: initial?.embed?.footer ?? '',
  author: initial?.embed?.author ?? '',
})

const embedFields: { key: Exclude<TextField, 'content'>; label: string; multiline?: boolean }[] = [
  { key: 'title', label: 'Title' },
  { key: 'url', label: 'Title link' },
  { key: 'description', label: 'Description', multiline: true },
  { key: 'author', label: 'Author' },
  { key: 'image', label: 'Image URL' },
  { key: 'thumbnail', label: 'Thumbnail URL' },
  { key: 'footer', label: 'Footer' },
]

const chips = computed(() => props.placeholders.filter((token) => !token.includes('<')))

const focused = ref<{ key: TextField; element: HTMLInputElement | HTMLTextAreaElement }>()

function track(key: TextField, event: FocusEvent) {
  focused.value = { key, element: event.target as HTMLInputElement | HTMLTextAreaElement }
}

function insert(token: string) {
  const target = focused.value ?? { key: 'content' as TextField, element: null }
  const current = text[target.key]
  const start = target.element?.selectionStart ?? current.length
  const end = target.element?.selectionEnd ?? current.length
  text[target.key] = current.slice(0, start) + token + current.slice(end)
  const element = target.element
  if (element) {
    requestAnimationFrame(() => {
      element.focus()
      element.setSelectionRange(start + token.length, start + token.length)
    })
  }
}

function optional(value: string): string | undefined {
  return value.trim() ? value : undefined
}

const value = computed<MessageTemplate | null>(() => {
  if (useDefault.value) {
    return null
  }
  const template: MessageTemplate = {}
  if (text.content.trim()) {
    template.content = text.content
  }
  if (useEmbed.value) {
    template.embed = {
      title: optional(text.title),
      description: optional(text.description),
      url: optional(text.url),
      image: optional(text.image),
      thumbnail: optional(text.thumbnail),
      footer: optional(text.footer),
      author: optional(text.author),
      color: useColor.value ? parseInt(color.value.slice(1), 16) : undefined,
      timestamp: timestamp.value || undefined,
    }
  }
  return template
})

watch(value, (template) => emit('update:modelValue', template), { deep: true })
</script>

<template>
  <div class="stack">
    <label class="check"><input v-model="useDefault" type="checkbox" /> Use the default template</label>
    <p v-if="useDefault" class="muted">
      Default: <code>{{ defaultContent.replace('\n', ' + new line + ') }}</code>
    </p>
    <template v-else>
      <div class="muted">
        Placeholders (click to insert into the last focused field):
        <span class="row" style="margin-top: 0.25rem">
          <button v-for="token in chips" :key="token" class="small" type="button" @mousedown.prevent @click="insert(token)">
            <code>{{ token }}</code>
          </button>
        </span>
        <span>
          <code v-text="domainToken"></code> lists only categories with that domain, e.g.
          <code v-text="typeToken"></code>. Markdown works in the text.
        </span>
      </div>
      <label>
        Message text
        <textarea v-model="text.content" rows="3" @focus="track('content', $event)"></textarea>
      </label>
      <label class="check"><input v-model="useEmbed" type="checkbox" /> Add an embed</label>
      <div v-if="useEmbed" class="grid">
        <label v-for="field in embedFields" :key="field.key" :style="field.multiline ? 'grid-column: 1 / -1' : ''">
          {{ field.label }}
          <textarea v-if="field.multiline" v-model="text[field.key]" rows="3" @focus="track(field.key, $event)"></textarea>
          <input v-else v-model="text[field.key]" type="text" @focus="track(field.key, $event)" />
        </label>
        <div class="row">
          <label class="check"><input v-model="useColor" type="checkbox" /> Color</label>
          <input v-if="useColor" v-model="color" type="color" style="width: 3rem; padding: 0" aria-label="Embed color" />
          <label class="check"><input v-model="timestamp" type="checkbox" /> Show publication date</label>
        </div>
      </div>
    </template>
    <ErrorList :errors="errors" />
  </div>
</template>
