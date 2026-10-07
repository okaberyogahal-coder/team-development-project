/**
 * @param {Record<string, Array<{address:string,family:string|number,internal:boolean}>|undefined>} interfaces
 * @param {number} pagePort
 * @param {number} plazaPort
 */
export function lanConfig(interfaces,pagePort=3002,plazaPort=3003){
 for(const port of [pagePort,plazaPort])if(!Number.isInteger(port)||port<1||port>65535)throw Error('Invalid LAN port');
 const addresses=new Set();
 for(const entries of Object.values(interfaces))for(const entry of entries || []){
  if(entry.internal || !['IPv4',4].includes(entry.family))continue;
  const parts=entry.address.split('.').map(Number);
  if(parts.length!==4 || parts.some(p=>!Number.isInteger(p)||p<0||p>255))continue;
  if(parts[0]===10 || (parts[0]===192 && parts[1]===168) || (parts[0]===172 && parts[1]>=16 && parts[1]<=31))addresses.add(entry.address);
 }
 const origins=new Set([`http://localhost:${pagePort}`,`http://127.0.0.1:${pagePort}`,'http://localhost:3000','http://127.0.0.1:3000']);
 for(const address of addresses)origins.add(`http://${address}:${pagePort}`);
 return {urls:Array.from(addresses,a=>`http://${a}:${pagePort}/plaza`),origins:Array.from(origins),pagePort,plazaPort};
}
