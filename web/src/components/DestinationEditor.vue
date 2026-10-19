<script setup lang="ts">
import { ref } from 'vue'
import type { Destination, FieldError, MessageTemplate } from '../api'
import { dateTime } from '../format'
import DiscordChannelPicker from './DiscordChannelPicker.vue'
import ErrorList from './ErrorList.vue'

/** Edits one destination in place: its sink and that sink's parameters. */
const props = defineProps<{
  destination: Destination
  sinks: string[]
  template: MessageTemplate | null
  errors: FieldError[]
  brokenSince?: string | null
  brokenReason?: string | null
}>()
const emit = defineEmits<{ remove: [] }>()

const newKey = ref('')

const knownParameters: Record<string, string[]> = { ntfy: ['topic'] }

function setSink(sink: string) {
  props.destination.sink = sink
  props.destination.parameters = Object.fromEntries((knownParameters[sink] ?? []).map((key) => [key, '']))
}

function addParameter() {
  const key = newKey.value.trim()
  if (key && !(key in props.destination.parameters)) {
    props.destination.parameters[key] = ''
  }
  newKey.value = ''
}

function removeParameter(key: string) {
  delete props.destination.parameters[key]
}
</script>

<template>
  <div class="card stack">
    <div class="row spread">
      <label class="row">
        Send to
        <select :value="destination.sink" style="width: auto" @change="setSink(($event.target as HTMLSelectElement).value)">
          <option v-if="!sinks.includes(destination.sink)" :value="destination.sink">{{ destination.sink }}</option>
          <option v-for="sink in sinks" :key="sink" :value="sink">{{ sink === 'discord' ? 'Discord' : sink }}</option>
        </select>
      </label>
      <button class="small danger" type="button" @click="emit('remove')">Remove</button>
    </div>
    <p v-if="brokenSince" class="notice error">
      Broken since {{ dateTime(brokenSince) }}: {{ brokenReason }}. Saving the route clears this flag and retries the destination.
    </p>
    <DiscordChannelPicker v-if="destination.sink === 'discord'" :key="destination.sink" :parameters="destination.parameters" :template="template" />
    <template v-else>
      <label v-for="(_, key) in destination.parameters" :key="key">
        {{ key }}
        <span class="row">
          <input v-model="destination.parameters[key]" type="text" style="flex: 1; width: auto" />
          <button class="small danger" type="button" @click="removeParameter(String(key))">&times;</button>
        </span>
      </label>
      <div class="row">
        <input v-model="newKey" type="text" placeholder="other parameter name" style="flex: 1; width: auto" @keydown.enter.prevent="addParameter" />
        <button class="small" type="button" @click="addParameter">Add parameter</button>
      </div>
    </template>
    <ErrorList :errors="errors" />
  </div>
</template>
