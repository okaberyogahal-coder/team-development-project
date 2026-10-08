'use client';
import {Component,ReactNode} from 'react';

// Also catches a failed dynamic chunk before the canvas can initialize.
export class WorldBoundary extends Component<{children:ReactNode;onFailure:()=>void},{failed:boolean}>{
 state={failed:false};
 static getDerivedStateFromError(){return {failed:true};}
 componentDidCatch(){this.props.onFailure();}
 render(){return this.state.failed?null:this.props.children;}
}
