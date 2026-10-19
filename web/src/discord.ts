import { api, type Channel, type Guild } from './api'

let guilds: Promise<Guild[]> | null = null
const channels = new Map<string, Promise<Channel[]>>()

function forget<T>(promise: Promise<T>, clear: () => void): Promise<T> {
  promise.catch(clear)
  return promise
}

/** Guilds the bot is in, shared by every destination picker on the page. */
export function loadGuilds(refresh = false): Promise<Guild[]> {
  if (refresh || guilds === null) {
    guilds = forget(api.guilds(refresh), () => (guilds = null))
  }
  return guilds
}

/** Channels of a guild that can be posted to, shared by every destination picker on the page. */
export function loadChannels(guildId: string, refresh = false): Promise<Channel[]> {
  let promise = channels.get(guildId)
  if (refresh || promise === undefined) {
    promise = forget(api.channels(guildId, refresh), () => channels.delete(guildId))
    channels.set(guildId, promise)
  }
  return promise
}
