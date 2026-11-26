import type {AgeBadge} from './profile-demographics';

export const plazaHeartbeatMs = 15_000;
export const plazaPresenceLifetimeMs = 45_000;

export type FriendConnectionState = 'none' | 'friend' | 'incoming' | 'outgoing';

export type PlazaPerson = {
  id: string;
  nickname: string;
  ageBadge: AgeBadge | null;
  teaches: string[];
  wantsToLearn: string[];
  connectionStatus: FriendConnectionState;
};
