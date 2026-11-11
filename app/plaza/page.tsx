import {getCurrentUser} from '../../server/auth/server';
import {redirect} from 'next/navigation';
import {ageBadge} from '../../shared/profile-demographics';
import type {Metadata} from 'next';
import './unity.css';
import { WorldFullscreen } from '../components/WorldFullscreen';
import { UnityExperience } from '../components/UnityExperience';
export const metadata:Metadata={title:'交流空間 | ミライフ',description:'アバターで集まり、世代を超えておしゃべりするミライフの交流空間。'};
export default async function PlazaPage(){
 const user=await getCurrentUser();
 if(user&&(!user.profile?.gender||!ageBadge(user.profile?.age))) redirect('/account/edit');
 return <main className="unity-page world-page">
  <WorldFullscreen><UnityExperience mode="world" /></WorldFullscreen>
 </main>;
}
