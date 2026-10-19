<script setup lang="ts">
import type { FilterCondition, FilterField, FilterGroup, FilterNode, FilterOperator } from '../api'

/**
 * Edits one node of a filter tree in place. Groups render their children recursively; `remove` asks the parent group
 * to drop this node.
 */
defineProps<{ node: FilterNode; root?: boolean }>()
const emit = defineEmits<{ remove: [] }>()

const fields: { value: FilterField; label: string }[] = [
  { value: 'title', label: 'Title' },
  { value: 'description', label: 'Description' },
  { value: 'author', label: 'Author' },
  { value: 'link', label: 'Link' },
  { value: 'category', label: 'Category' },
]

const operators: { value: FilterOperator; label: string }[] = [
  { value: 'contains', label: 'contains' },
  { value: 'equals', label: 'equals' },
  { value: 'startsWith', label: 'starts with' },
  { value: 'regex', label: 'matches regex' },
]

function newCondition(): FilterCondition {
  return { type: 'condition', field: 'title', operator: 'contains', value: '' }
}

function addCondition(group: FilterGroup) {
  group.children.push(newCondition())
}

function addGroup(group: FilterGroup) {
  group.children.push({ type: group.type === 'all' ? 'any' : 'all', children: [newCondition()] })
}

function removeChild(group: FilterGroup, index: number) {
  group.children.splice(index, 1)
}

function setField(condition: FilterCondition, field: FilterField) {
  condition.field = field
  if (field !== 'category') {
    delete condition.domain
  }
}

function setDomain(condition: FilterCondition, domain: string) {
  if (domain.trim()) {
    condition.domain = domain.trim()
  } else {
    delete condition.domain
  }
}

function asGroup(node: FilterNode): FilterGroup | null {
  return node.type === 'condition' ? null : node
}

function asCondition(node: FilterNode): FilterCondition | null {
  return node.type === 'condition' ? node : null
}

</script>

<template>
  <div v-if="asGroup(node)" class="filter-group" :class="{ root }">
    <div class="row">
      <label class="check"><input v-model="node.negate" type="checkbox" /> NOT</label>
      <select v-model="asGroup(node)!.type" style="width: auto" aria-label="Group operator">
        <option value="all">All of these (AND)</option>
        <option value="any">Any of these (OR)</option>
      </select>
      <span style="flex: 1"></span>
      <button v-if="!root" class="small danger" type="button" @click="emit('remove')">Remove group</button>
    </div>
    <div class="filter-children">
      <FilterNodeEditor
        v-for="(child, index) in asGroup(node)!.children"
        :key="index"
        :node="child"
        @remove="removeChild(asGroup(node)!, index)"
      />
      <p v-if="asGroup(node)!.children.length === 0" class="muted">
        {{ root ? 'Empty: every item passes.' : 'An empty group is not allowed; add a condition or remove it.' }}
      </p>
    </div>
    <div class="row">
      <button class="small" type="button" @click="addCondition(asGroup(node)!)">+ Condition</button>
      <button class="small" type="button" @click="addGroup(asGroup(node)!)">+ Group</button>
    </div>
  </div>

  <div v-else-if="asCondition(node)" class="filter-condition row">
    <label class="check" title="Invert this condition"><input v-model="node.negate" type="checkbox" /> NOT</label>
    <select
      :value="asCondition(node)!.field"
      aria-label="Field"
      @change="setField(asCondition(node)!, ($event.target as HTMLSelectElement).value as FilterField)"
    >
      <option v-for="field in fields" :key="field.value" :value="field.value">{{ field.label }}</option>
    </select>
    <input
      v-if="asCondition(node)!.field === 'category'"
      :value="asCondition(node)!.domain ?? ''"
      type="text"
      placeholder="domain (optional)"
      aria-label="Category domain"
      @input="setDomain(asCondition(node)!, ($event.target as HTMLInputElement).value)"
    />
    <select v-model="asCondition(node)!.operator" aria-label="Operator">
      <option v-for="operator in operators" :key="operator.value" :value="operator.value">{{ operator.label }}</option>
    </select>
    <input v-model="asCondition(node)!.value" type="text" placeholder="value" aria-label="Value" class="filter-value" />
    <label class="check"><input v-model="asCondition(node)!.caseSensitive" type="checkbox" /> Aa</label>
    <button class="small danger" type="button" aria-label="Remove condition" @click="emit('remove')">&times;</button>
  </div>
</template>

<style scoped>
.filter-group {
  border-left: 3px solid var(--border);
  padding: 0.25rem 0 0.25rem 0.75rem;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.filter-group.root {
  border-left: none;
  padding-left: 0;
}

.filter-children {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.filter-condition select,
.filter-condition input[type='text'] {
  width: auto;
  flex: 1 1 8rem;
}

.filter-condition .filter-value {
  flex: 2 1 12rem;
}
</style>
