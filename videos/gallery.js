(function(){
  var grid=document.getElementById('grid');
  function fmt(s){s=Math.round(s||0);var m=Math.floor(s/60),r=s%60;return m+':'+(r<10?'0':'')+r;}
  function el(t,c,txt){var e=document.createElement(t);if(c)e.className=c;if(txt!=null)e.textContent=txt;return e;}
  function abs(base,p){return /^https?:/.test(p)?p:base.replace(/\/$/,'')+'/'+p;}
  fetch('/videos/videos.json',{cache:'no-cache'}).then(function(r){return r.json();}).then(function(d){
    grid.textContent='';
    (d.videos||[]).forEach(function(v){
      var a=el('a','card');a.href='/videos/'+v.slug+'/';
      var th=el('div','thumb');
      if(v.status==='published'&&v.thumb){th.style.backgroundImage='url("'+abs(d.media_base,v.thumb)+'")';th.appendChild(el('span','dur',fmt(v.duration)));}
      else th.appendChild(el('span','soon','Coming soon'));
      var b=el('div','body');
      b.appendChild(el('p','status',v.status==='published'?(v.audience||'Video'):'Coming soon'));
      b.appendChild(el('h2',null,v.title));
      b.appendChild(el('p','meta',[v.duration?'Runtime '+fmt(v.duration):'',v.audience||''].filter(Boolean).join(' · ')));
      a.appendChild(th);a.appendChild(b);grid.appendChild(a);
    });
    if(!grid.children.length)grid.appendChild(el('p','meta','No videos yet.'));
  }).catch(function(){grid.textContent='';grid.appendChild(el('p','err','Could not load the video list.'));});
})();
