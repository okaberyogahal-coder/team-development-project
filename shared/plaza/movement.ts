import type {Direction,Position,RoomId} from './types';
import {WALLS,WORLD} from './world';
export function advance(position:Position,direction:Direction,dtMs:number,room:RoomId):Position {
 const length=Math.hypot(direction.x,direction.y);
 if(!length || !Number.isFinite(length)) return {...position};
 const distance=WORLD.speed*Math.min(Math.max(dtMs,0),100)/1000;
 const clamp=(v:number,max:number)=>Math.max(WORLD.padding,Math.min(max-WORLD.padding,v));
 const blocked=(x:number,y:number)=>room==='lobby' && WALLS.some(w=>x>w.x-12 && x<w.x+w.width+12 && y>w.y-12 && y<w.y+w.height+12);
 let x=clamp(position.x+direction.x/length*distance,WORLD.width);
 let y=clamp(position.y+direction.y/length*distance,WORLD.height);
 if(blocked(x,position.y)) x=position.x;
 if(blocked(x,y)) y=position.y;
 return {x,y};
}
