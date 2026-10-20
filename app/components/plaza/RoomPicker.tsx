import {Icon} from "../Icon";
import {ROOMS} from '../../../shared/plaza/world';
import type {RoomId,RoomSummary} from '../../../shared/plaza/types';
export function RoomPicker({roomId,rooms,onChange,disabled}:{roomId:RoomId;rooms:RoomSummary[];onChange:(id:RoomId)=>void;disabled:boolean}){
 return <><div className="plaza-section-caption">ROOMS <span>好きな部屋へ</span></div><div className="plaza-room-list">{ROOMS.map(r=>{
  const count=rooms.find(s=>s.id===r.id)?.count || 0;const current=r.id===roomId;const full=r.id!=='lobby' && count>=r.capacity;
  return <button key={r.id} className={`plaza-room-button ${current?'active':''}`} onClick={()=>onChange(r.id)} disabled={disabled || current || full} aria-current={current?'location':undefined}>
   <span className="plaza-room-icon" style={{background:r.color+'22'}}><Icon name={r.icon} /></span><span className="plaza-room-label"><strong>{r.name}</strong><span>{current?'いまここにいます':full?'満員です':r.id==='lobby'?'待ち合わせ・自由に移動':'部屋に入ると会話できます'}</span></span><span className="plaza-room-count">{count}<small>/{r.capacity}</small></span>
  </button>;
 })}</div></>;
}
