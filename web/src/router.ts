import { createRouter, createWebHistory } from 'vue-router'
import FeedListView from './views/FeedListView.vue'
import FeedView from './views/FeedView.vue'
import RouteEditorView from './views/RouteEditorView.vue'
import SettingsView from './views/SettingsView.vue'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', name: 'feeds', component: FeedListView },
    { path: '/feeds/:feedId', name: 'feed', component: FeedView, props: true },
    { path: '/feeds/:feedId/routes/new', name: 'new-route', component: RouteEditorView, props: true },
    { path: '/routes/:routeId', name: 'route', component: RouteEditorView, props: true },
    { path: '/settings', name: 'settings', component: SettingsView },
    { path: '/:pathMatch(.*)*', redirect: '/' },
  ],
})
