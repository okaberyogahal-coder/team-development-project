(function(){
 'use strict';
 var ready=false,catalog=null,pending=null,revision=0;
 function send(value){if(parent!==window)parent.postMessage(value,location.origin);}
 function apply(){if(!ready||!window.unityInstance||!pending)return;window.unityInstance.SendMessage('MiraifuFaceSeedWeb','ApplyJson',JSON.stringify(pending.choice));}
 window.faceSeedCatalog=function(json){catalog=JSON.parse(json);send({type:'miraifu:avatar-catalog',catalog:catalog});};
 window.faceSeedReady=function(){ready=true;document.body.dataset.unityState='ready';document.getElementById('status').hidden=true;send({type:'miraifu:avatar-ready'});apply();};
 window.faceSeedState=function(json){var state=JSON.parse(json);window.miraifuAppliedAvatar=state;send({type:'miraifu:avatar-state',state:state,revision:revision});};
 window.avatarInstanceReady=function(){if(ready){send({type:'miraifu:avatar-ready'});apply();}};
 addEventListener('message',function(event){
  if(parent===window||event.source!==parent||event.origin!==location.origin)return;
  if(event.data?.type==='miraifu:avatar'&&event.data.choice&&typeof event.data.choice==='object'){
   pending={choice:event.data.choice};revision=Number.isSafeInteger(event.data.revision)?event.data.revision:0;apply();
  }else if(event.data?.type==='miraifu:catalog-request'){if(catalog)send({type:'miraifu:avatar-catalog',catalog:catalog});if(ready)send({type:'miraifu:avatar-ready'});}
 });
}());
