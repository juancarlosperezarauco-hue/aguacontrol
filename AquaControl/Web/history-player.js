'use strict';
// Recorded positions only: a long GPS gap is stepped over, never interpolated into a fictional location.
class FieldHistoryPlayer {
 constructor({count,onFrame,onState,delay=1000}){this.count=count;this.onFrame=onFrame;this.onState=onState;this.delay=delay;this.index=0;this.timer=null;this.playing=false;}
 state(){this.onState?.({index:this.index,count:this.count,playing:this.playing});}
 seek(index){this.index=Math.max(0,Math.min(Math.max(0,this.count-1),Number(index)||0));if(this.count)this.onFrame(this.index);this.state();}
 play(){if(this.playing||!this.count)return;if(this.index>=this.count-1)this.seek(0);this.playing=true;this.state();this.schedule();}
 schedule(){clearTimeout(this.timer);if(!this.playing)return;this.timer=setTimeout(()=>{if(this.index>=this.count-1){this.pause();return;}this.seek(this.index+1);if(this.index>=this.count-1)this.pause();else this.schedule();},this.delay);}
 pause(){this.playing=false;clearTimeout(this.timer);this.timer=null;this.state();}
 speed(multiplier){this.delay=1000/Math.max(.25,Math.min(8,Number(multiplier)||1));if(this.playing)this.schedule();}
 restart(){this.pause();this.seek(0);}
 dispose(){this.onFrame=()=>{};this.onState=null;this.pause();}
}
