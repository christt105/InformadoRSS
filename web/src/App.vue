<script setup lang="ts">
import { RouterLink, RouterView } from 'vue-router'
</script>

<template>
  <header class="topbar">
    <RouterLink to="/" class="brand">InformadoRSS</RouterLink>
    <nav>
      <RouterLink to="/">Feeds</RouterLink>
      <RouterLink to="/settings">Settings</RouterLink>
    </nav>
  </header>
  <main class="page">
    <RouterView />
  </main>
</template>
