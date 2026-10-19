<script setup lang="ts">
import type { FieldError } from '../api'

defineProps<{ errors: FieldError[] | null | undefined }>()
</script>

<template>
  <ul v-if="errors && errors.length > 0" class="errors">
    <li v-for="(error, index) in errors" :key="index">
      <code v-if="error.path && error.path !== '$'">{{ error.path }}</code>
      {{ error.message }}
    </li>
  </ul>
</template>
