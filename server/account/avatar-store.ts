import type {PrismaClient, Prisma} from '../../generated/prisma/client';
import type {AvatarAppearance} from '../../shared/demo-profile';
import type {AccountAvatar} from '../../shared/account-avatar-save';
import {validAppearance} from '../../shared/demo-profile';
import {ProfileAccessError, ProfileConflictError} from './profile-handler';

export function accountAvatarForProfile(userId: string, profile: {avatarAppearance: unknown; avatarVersion: number} | null): AccountAvatar {
  return {ownerId: userId, appearance: validAppearance(profile?.avatarAppearance) ? profile.avatarAppearance : null, version: profile?.avatarVersion ?? 0};
}

export async function readAccountAvatar(database: Pick<Prisma.TransactionClient, 'userProfile'>, userId: string): Promise<AccountAvatar> {
  const profile = await database.userProfile.findUnique({where: {userId}, select: {avatarAppearance: true, avatarVersion: true}});
  return accountAvatarForProfile(userId, profile);
}

export async function saveAccountAvatar(database: PrismaClient, userId: string, appearance: AvatarAppearance, version: number): Promise<AccountAvatar> {
  return database.$transaction(async tx => {
    const active = await tx.user.updateMany({where: {id: userId, deletedAt: null, status: {not: 'SUSPENDED'}}, data: {updatedAt: new Date()}});
    if (active.count !== 1) throw new ProfileAccessError();
    const existing = await tx.userProfile.findUnique({where: {userId}, select: {userId: true}});
    if (!existing) {
      if (version !== 0) throw new ProfileConflictError();
      const user = await tx.user.findUniqueOrThrow({where: {id: userId}, select: {name: true}});
      await tx.userProfile.create({data: {userId, nickname: user.name || '利用者', avatarAppearance: appearance as unknown as Prisma.InputJsonValue, avatarVersion: 1}});
      return readAccountAvatar(tx, userId);
    }
    const result = await tx.userProfile.updateMany({
      where: {userId, avatarVersion: version},
      data: {avatarAppearance: appearance as unknown as Prisma.InputJsonValue, avatarVersion: {increment: 1}},
    });
    if (result.count !== 1) throw new ProfileConflictError();
    return readAccountAvatar(tx, userId);
  });
}
