import type {Metadata} from 'next';
import {PlazaClient} from '../components/plaza/PlazaClient';
import './plaza.css';
export const metadata:Metadata={title:'交流空間 | ミライフ',description:'アバターで集まり、世代を超えておしゃべりするミライフの交流空間。'};
export default function PlazaPage(){return <PlazaClient/>;}
