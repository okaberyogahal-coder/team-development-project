import type {Metadata} from 'next';
import {AuthShell} from '../../components/AuthShell';
import {ProfileEditForm} from '../../components/ProfileEditForm';
export const metadata:Metadata={title:'プロフィール編集 | ミライフ ME LIFE',robots:{index:false,follow:false}};
export default function ProfileEditPage(){return <AuthShell><ProfileEditForm/></AuthShell>;}
