import {getCurrentUser} from '../../../../server/auth/server';
import {getDatabase} from '../../../../server/db/client';
import {allowAuthAttempt} from '../../../../server/auth/http';
import {createProfileHandlers} from '../../../../server/account/profile-handler';
import {readAccountProfile,saveAccountProfile} from '../../../../server/account/profile-store';
export const runtime='nodejs';
export const dynamic='force-dynamic';
export const {GET,PATCH}=createProfileHandlers({
 getUser:getCurrentUser,
 read:id=>readAccountProfile(getDatabase(),id),
 save:(id,input)=>saveAccountProfile(getDatabase(),id,input),
 allowSave:id=>allowAuthAttempt(getDatabase(),'profile-edit',id,20),
});
