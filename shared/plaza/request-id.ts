type BrowserCrypto=Pick<Crypto,'getRandomValues'> & Partial<Pick<Crypto,'randomUUID'>>;
export function createRoomRequestId(cryptoLike:BrowserCrypto=globalThis.crypto):string {
 if(typeof cryptoLike.randomUUID==='function')return cryptoLike.randomUUID();
 // getRandomValues is available on LAN HTTP, where randomUUID requires HTTPS.
 const bytes=cryptoLike.getRandomValues(new Uint8Array(16));
 return Array.from(bytes,b=>b.toString(16).padStart(2,'0')).join('');
}
