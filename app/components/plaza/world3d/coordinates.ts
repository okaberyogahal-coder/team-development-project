import type {Position} from '../../../../shared/plaza/types';

export function toWorld(p:Position){return {x:(p.x-500)/50,y:0,z:(p.y-350)/50};}

export function interpolatePosition(current:Position,target:Position,dtMs:number):Position{
 const amount=Math.min(Math.max(dtMs,0)/100,1);
 return {x:current.x+(target.x-current.x)*amount,y:current.y+(target.y-current.y)*amount};
}

// Models face +z. The server measures facing from +x towards +y (floor +z).
export function facingToRotation(facing:number){return Math.PI/2-facing;}
