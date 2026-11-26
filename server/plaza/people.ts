import 'server-only';
import type {PrismaClient} from '../../generated/prisma/client';
import {ageBadge} from '../../shared/profile-demographics';
import {plazaPresenceLifetimeMs, type PlazaPerson} from '../../shared/plaza-people';
import {readFriendConnectionStates} from '../friends/store';

export async function refreshPlazaPeople(database: PrismaClient, userId: string, sessionId: string): Promise<PlazaPerson[]> {
  const now = new Date();
  await database.plazaPresence.upsert({
    where: {sessionId},
    create: {sessionId, lastSeenAt: now},
    update: {lastSeenAt: now},
  });
  const people = await database.user.findMany({
    where: {
      id: {not: userId},
      deletedAt: null,
      status: {not: 'SUSPENDED'},
      profile: {is: {gender: {in: ['male', 'female']}, age: {gte: 18, lte: 130}}},
      authSessions: {
        some: {
          expiresAt: {gt: now},
          plazaPresence: {is: {lastSeenAt: {gt: new Date(now.getTime() - plazaPresenceLifetimeMs)}}},
        },
      },
    },
    select: {id: true, profile: {select: {nickname: true, age: true, teaches: true, wantsToLearn: true}}},
    orderBy: [{profile: {nickname: 'asc'}}, {id: 'asc'}],
  });
  const states = await readFriendConnectionStates(database, userId, people.map(person => person.id));
  return people.flatMap(({id, profile}) => profile ? [{
    id,
    nickname: profile.nickname,
    ageBadge: ageBadge(profile.age),
    teaches: profile.teaches,
    wantsToLearn: profile.wantsToLearn,
    connectionStatus: states.get(id) ?? 'none',
  }] : []);
}
