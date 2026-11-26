import {getCurrentUser} from '../../../../server/auth/server';
import {getDatabase} from '../../../../server/db/client';
import {allowAuthAttempt} from '../../../../server/auth/http';
import {createAvatarHandlers} from '../../../../server/account/avatar-handler';
import {readAccountAvatar, saveAccountAvatar} from '../../../../server/account/avatar-store';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';
export const {GET, PUT} = createAvatarHandlers({
  getUser: getCurrentUser,
  read: id => readAccountAvatar(getDatabase(), id),
  save: (id, appearance, version) => saveAccountAvatar(getDatabase(), id, appearance, version),
  allowSave: id => allowAuthAttempt(getDatabase(), 'avatar-save', id, 20),
});
