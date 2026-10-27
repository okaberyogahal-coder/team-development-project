import type {Metadata} from 'next';
import './unity.css';
import { WorldFullscreen } from '../components/WorldFullscreen';
import { UnityExperience } from '../components/UnityExperience';
export const metadata:Metadata={title:'交流空間 | ミライフ',description:'アバターで集まり、世代を超えておしゃべりするミライフの交流空間。'};
export default function PlazaPage(){
 return <main className="unity-page world-page">
  <WorldFullscreen><UnityExperience mode="world" /></WorldFullscreen>
 </main>;
}
