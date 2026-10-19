<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import {
  api,
  ApiRequestError,
  errorMessage,
  type DestinationState,
  type FieldError,
  type FilterGroup,
  type MessageTemplate,
  type Meta,
  type RouteInput,
  type RouteValidation,
} from '../api'
import DestinationEditor from '../components/DestinationEditor.vue'
import ErrorList from '../components/ErrorList.vue'
import FilterNodeEditor from '../components/FilterNodeEditor.vue'
import TemplateEditor from '../components/TemplateEditor.vue'

const props = defineProps<{ feedId?: string; routeId?: string }>()
const router = useRouter()

const meta = ref<Meta | null>(null)
const feedName = ref('')
const ownerFeedId = ref<number | null>(props.feedId ? Number(props.feedId) : null)
const loaded = ref(false)
const loadError = ref<string | null>(null)

const name = ref('')
const enabled = ref(true)
const filtered = ref(false)
const filterRoot = reactive<FilterGroup>({ type: 'all', children: [] })
const template = ref<MessageTemplate | null>(null)
const destinations = ref<DestinationState[]>([])

const validation = ref<RouteValidation | null>(null)
const saveError = ref<string | null>(null)
const saving = ref(false)

const input = computed<RouteInput>(() => ({
  name: name.value,
  enabled: enabled.value,
  filter: filtered.value ? filterRoot : null,
  template: template.value,
  destinations: destinations.value.map((d) => ({ sink: d.sink, parameters: { ...d.parameters } })),
}))

const generalErrors = computed(() => validation.value?.errors.filter((e) => !e.path.startsWith('$.destinations[')) ?? [])

function destinationErrors(index: number): FieldError[] {
  const prefix = `$.destinations[${index}]`
  return (
    validation.value?.errors
      .filter((e) => e.path.startsWith(prefix))
      .map((e) => ({ path: e.path.slice(prefix.length).replace(/^\./, '') || '$', message: e.message })) ?? []
  )
}

function addDestination() {
  const sink = meta.value?.sinks.includes('discord') ? 'discord' : (meta.value?.sinks[0] ?? 'discord')
  destinations.value.push({ sink, parameters: sink === 'ntfy' ? { topic: '' } : {}, brokenSince: null, brokenReason: null })
}

const keys = new WeakMap<DestinationState, number>()
let nextKey = 0
function keyOf(destination: DestinationState): number {
  let key = keys.get(destination)
  if (key === undefined) {
    key = nextKey++
    keys.set(destination, key)
  }
  return key
}

let timer: ReturnType<typeof setTimeout> | undefined
watch(
  input,
  (value) => {
    if (!loaded.value) {
      return
    }
    clearTimeout(timer)
    timer = setTimeout(async () => {
      try {
        validation.value = await api.validateRoute(value)
      } catch {
        validation.value = null
      }
    }, 400)
  },
  { deep: true },
)

async function load() {
  try {
    meta.value = await api.meta()
    if (props.routeId) {
      const route = await api.route(Number(props.routeId))
      ownerFeedId.value = route.feedId
      name.value = route.name
      enabled.value = route.enabled
      if (route.filter) {
        filtered.value = true
        Object.assign(filterRoot, route.filter.type === 'condition' ? { type: 'all', negate: false, children: [route.filter] } : route.filter)
      }
      template.value = route.template
      destinations.value = route.destinations
    } else {
      name.value = 'All items'
      addDestination()
    }
    if (ownerFeedId.value !== null) {
      feedName.value = (await api.feed(ownerFeedId.value)).name
    }
    loaded.value = true
  } catch (e) {
    loadError.value = errorMessage(e)
  }
}

async function save() {
  saving.value = true
  saveError.value = null
  try {
    if (props.routeId) {
      await api.updateRoute(Number(props.routeId), input.value)
    } else if (ownerFeedId.value !== null) {
      await api.createRoute(ownerFeedId.value, input.value)
    }
    router.push({ name: 'feed', params: { feedId: String(ownerFeedId.value) } })
  } catch (e) {
    saveError.value = errorMessage(e)
    if (e instanceof ApiRequestError && e.body) {
      validation.value = {
        valid: false,
        errors: e.body.errors ?? [],
        filterErrors: e.body.filterErrors ?? [],
        templateErrors: e.body.templateErrors ?? [],
      }
    }
  } finally {
    saving.value = false
  }
}

async function remove() {
  if (!props.routeId || !confirm(`Delete the route "${name.value}"?`)) {
    return
  }
  try {
    await api.deleteRoute(Number(props.routeId))
    router.push({ name: 'feed', params: { feedId: String(ownerFeedId.value) } })
  } catch (e) {
    saveError.value = errorMessage(e)
  }
}

onMounted(load)
</script>

<template>
  <p v-if="ownerFeedId !== null">
    <RouterLink :to="{ name: 'feed', params: { feedId: String(ownerFeedId) } }">&larr; {{ feedName || 'Feed' }}</RouterLink>
  </p>
  <p v-if="loadError" class="notice error">{{ loadError }}</p>
  <form v-if="loaded && meta" class="stack" @submit.prevent="save">
    <h1>{{ routeId ? 'Edit route' : 'New route' }}</h1>

    <section class="card stack">
      <div class="grid">
        <label>Name <input v-model="name" type="text" maxlength="200" required /></label>
        <label class="check" style="align-self: end"><input v-model="enabled" type="checkbox" /> Enabled</label>
      </div>
      <ErrorList :errors="generalErrors" />
    </section>

    <section class="card stack">
      <h2>Filter</h2>
      <div class="row">
        <label class="check"><input v-model="filtered" type="radio" :value="false" /> Send every item</label>
        <label class="check"><input v-model="filtered" type="radio" :value="true" /> Only items that match</label>
      </div>
      <template v-if="filtered">
        <FilterNodeEditor :node="filterRoot" root />
        <p class="muted">
          Comparisons ignore case unless <strong>Aa</strong> is checked. Category conditions can be limited to a domain, e.g.
          <code>type</code> in <code>&lt;category domain="type"&gt;movie&lt;/category&gt;</code>.
        </p>
      </template>
      <ErrorList :errors="validation?.filterErrors" />
    </section>

    <section class="card stack">
      <h2>Message</h2>
      <TemplateEditor
        v-model="template"
        :errors="validation?.templateErrors ?? []"
        :placeholders="meta.placeholders"
        :default-content="meta.defaultTemplateContent"
      />
    </section>

    <section class="stack">
      <div class="row spread">
        <h2 style="margin: 0">Destinations</h2>
        <button class="small" type="button" @click="addDestination">+ Destination</button>
      </div>
      <p v-if="destinations.length === 0" class="muted">No destinations: the route does nothing until you add one.</p>
      <DestinationEditor
        v-for="(destination, index) in destinations"
        :key="keyOf(destination)"
        :destination="destination"
        :sinks="meta.sinks"
        :template="template"
        :errors="destinationErrors(index)"
        :broken-since="destination.brokenSince"
        :broken-reason="destination.brokenReason"
        @remove="destinations.splice(index, 1)"
      />
    </section>

    <div class="row">
      <button class="primary" type="submit" :disabled="saving">{{ saving ? 'Saving...' : 'Save route' }}</button>
      <span v-if="validation && !validation.valid" class="bad">Fix the problems above before saving.</span>
      <span style="flex: 1"></span>
      <button v-if="routeId" class="danger" type="button" @click="remove">Delete route</button>
    </div>
    <p v-if="saveError" class="notice error">{{ saveError }}</p>
  </form>
</template>
