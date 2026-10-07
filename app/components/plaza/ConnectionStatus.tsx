export function ConnectionStatus({status,total}:{status:string;total:number}){
 return <div className="plaza-live-status" role="status"><span className={`plaza-dot ${status==='joined'?'':'waiting'}`}/>{status==='joined'?<>オンライン <span className="plaza-status-divider"/>{total}人が参加中</>:status==='reconnecting'?'再接続中…':status==='connecting'?'接続中…':'ゲストで参加できます'}</div>;
}
