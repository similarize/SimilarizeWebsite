// Data-driven watch page: slug comes from /videos/<slug>/, data from /videos/videos.json.
// hls.js (self-hosted, /videos/lib) where MSE exists; native HLS on iOS/Safari otherwise.
(function(){
  var slug=(location.pathname.match(/\/videos\/([a-z0-9-]+)\/?/)||[])[1];
  var box=document.getElementById('player'),T=document.getElementById('title'),
      D=document.getElementById('desc'),M=document.getElementById('meta'),Q=document.getElementById('quality');
  function abs(base,p){return /^https?:/.test(p)?p:base.replace(/\/$/,'')+'/'+p;}
  function placeholder(h,t){box.innerHTML='';var p=document.createElement('div');p.className='placeholder';
    var s=document.createElement('strong');s.textContent=h;p.appendChild(s);var x=document.createElement('span');x.textContent=t;p.appendChild(x);box.appendChild(p);}
  function load(src,cb){var s=document.createElement('script');s.src=src;s.onload=function(){cb();};s.onerror=function(){cb(new Error('load'));};document.head.appendChild(s);}
  fetch('/videos/videos.json',{cache:'no-cache'}).then(function(r){return r.json();}).then(function(d){
    var v=(d.videos||[]).filter(function(x){return x.slug===slug;})[0];
    if(!v){placeholder('Video not found','Try the video list.');return;}
    T.textContent=v.title;document.title=v.title+' — Similarize';D.textContent=v.description||'';
    function fmt(t){t=Math.round(t);var m=Math.floor(t/60),r=t%60;return m+':'+(r<10?'0':'')+r;}
    M.textContent=[v.duration?'Runtime '+fmt(v.duration):'',v.audience||''].filter(Boolean).join(' · ');
    if(v.badge){var bd=document.createElement('span');bd.className='badge';bd.textContent=v.badge;M.insertBefore(bd,M.firstChild);}
    (v.related||[]).forEach(function(r){var o=(d.videos||[]).filter(function(x){return x.slug===r.slug;})[0];if(!o)return;
      var p=document.createElement('p');p.className='also';var a=document.createElement('a');a.href='/videos/'+o.slug+'/';
      a.textContent=(r.label||'Also watch')+' \u2192';a.title=o.title;p.appendChild(a);D.parentNode.insertBefore(p,D.nextSibling);});
    if(v.status!=='published'||!v.hls){placeholder('Coming soon','This movie is being finished. Check back shortly.');return;}
    var src=abs(d.media_base,v.hls);
    var video=document.createElement('video');
    video.controls=true;video.preload='metadata';video.playsInline=true;video.setAttribute('playsinline','');
    video.setAttribute('crossorigin','anonymous');if(v.poster)video.poster=abs(d.media_base,v.poster);
    video.setAttribute('aria-label',v.title);box.innerHTML='';box.appendChild(video);
    function fail(){placeholder('Playback problem','The video could not be loaded. Please try again later.');}
    load('/videos/lib/hls.light.min.js',function(err){
      var H=window.Hls;
      if(!err&&H&&H.isSupported()){
        var hls=new H({autoStartLoad:false,capLevelToPlayerSize:true,maxBufferLength:30});
        hls.loadSource(src);hls.attachMedia(video);
        var started=false;function start(){if(!started){started=true;hls.startLoad(-1);}}
        video.addEventListener('play',start);
        hls.on(H.Events.MANIFEST_PARSED,function(_,data){
          Q.innerHTML='';var o=document.createElement('option');o.value='-1';o.textContent='Auto';Q.appendChild(o);
          hls.levels.forEach(function(l,i){var e=document.createElement('option');e.value=i;e.textContent=l.height+'p';Q.appendChild(e);});
          Q.hidden=false;Q.onchange=function(){hls.currentLevel=+Q.value;};
        });
        var tries=0;
        hls.on(H.Events.ERROR,function(_,e){if(!e.fatal)return;
          if(e.type===H.ErrorTypes.NETWORK_ERROR&&tries++<3){setTimeout(function(){hls.startLoad();},1000*tries);}
          else if(e.type===H.ErrorTypes.MEDIA_ERROR&&tries++<3){hls.recoverMediaError();}
          else{hls.destroy();fail();}});
        window.__videoHls=hls;
      } else if(video.canPlayType('application/vnd.apple.mpegurl')){
        video.src=src;video.addEventListener('error',fail);
      } else fail();
    });
  }).catch(function(){placeholder('Could not load','The video list did not load.');});
})();
