const fs=require('fs');
const S='file:Required/Scripts/';
const L=[
 ['layer:gesture-detection','Gesture Detection & Recognition',
  'Single-finger gesture detectors and stroke recognisers - the components a developer drops in to turn raw finger lifecycle (down, up, held, tap, flick, swipe, edge swipe) into UnityEvents, plus the shape-matching, gesture-arbitration and stroke-replay pieces built on top of the same stroke data.',
  ['LeanFingerFlick','LeanFingerHeld','LeanFingerTapExpired','LeanFingerTapQuick','LeanFirstDown','LeanLastUp','LeanManualFlick','LeanManualSwipe','LeanSwipeEdge','LeanShape','LeanShapeDetector','LeanGestureToggle','LeanReplayFinger']],
 ['layer:multi-finger-gestures','Multi-Finger Gestures',
  'The LeanMulti* family: components that filter a set of simultaneous fingers and emit a measured value - pinch ratio, twist angle, directional distance, pull offset, per-frame delta - or fire on multi-finger down/up/held/tap/swipe. Grouped because they all share the same finger-filter configuration and float/Vector event contract designers wire in the Inspector.',
  ['LeanMultiDirection','LeanMultiDown','LeanMultiHeld','LeanMultiPinch','LeanMultiPull','LeanMultiSwipe','LeanMultiTap','LeanMultiTwist','LeanMultiUp','LeanMultiUpdate']],
 ['layer:selection','Selection & Picking',
  'Everything that answers "which object did the player choose": the drag-select and selection-box multi-selection tools, the lightweight LeanPick/LeanPickable raycast pair, and the LeanSelectable companion behaviours (dial, torque, pressure scale, centroid, hold/selected timers, the match-3 grid block reference). LeanSelectSelf.cs lives here too - it is a gutted 26-byte stub with no code and no edges, but its name and its surviving editor class belong to this selection cluster, so this is where a developer would look for it.',
  ['LeanDragSelect','LeanPick','LeanPickable','LeanSelectionBox','LeanSelectSelf','LeanSelectableBlock','LeanSelectableCenter','LeanSelectableDial','LeanSelectableDragTorque','LeanSelectablePressureScale','LeanSelectableSelected','LeanSelectableTime']],
 ['layer:drag-and-drop','Drag & Drop',
  'The only interface-based cluster in the package: IDropHandler defines the drop-target contract, LeanDrop and LeanDropCount implement it (raising dropped/match/unmatch events), and LeanSelectableDrop raycasts under a released selectable to find a handler. These four are genuinely coupled in code rather than only through the Inspector.',
  ['IDropHandler','LeanDrop','LeanDropCount','LeanSelectableDrop']],
 ['layer:manipulation','Transform, Physics & Mesh Manipulation',
  'Drag-driven manipulation of scene content: translating Transforms and Rigidbodies (3D, 2D, along a surface or path), deforming and vertex-colouring meshes, rendering a drag as a line, and spawning a prefab under the finger. Grouped by effect - these are the components that actually change the world, as opposed to the ones that only report input.',
  ['LeanDragTranslateAlong','LeanDragTranslateAlongRigidbody','LeanDragTranslateRigidbody','LeanDragTranslateRigidbody2D','LeanDragColorMesh','LeanDragDeformMesh','LeanDragLine','LeanSpawnWithFinger']],
 ['layer:ui-canvas','Canvas / UI Input Variants',
  'The five *Canvas subclasses, kept together rather than filed with their world-space base classes because they share one distinct concern: restricting finger detection to the RectTransform they are attached to via Unity UI raycasting. They are the only components in the package coupled to the UI system, and a developer choosing between world-space and canvas-space input makes that decision once, here.',
  ['LeanFingerDownCanvas','LeanFingerHeldCanvas','LeanFirstDownCanvas','LeanLastUpCanvas','LeanMultiUpdateCanvas']],
 ['layer:camera-control','Camera & Viewport Control',
  'Gesture-to-camera components: pinch zoom (orthographic size or field of view, with damping and clamping), two-finger twist rotation, and mouse-wheel input as its desktop equivalent. Separate from the gesture detectors because these consume a gesture and drive the camera themselves instead of exposing an event.',
  ['LeanPinchCamera','LeanTwistCamera','LeanMouseWheel']],
];
const extra=['config:LeanTouchPlus.asmdef','config:package.json','document:Required/Documentation.html'];
const out=L.map(([id,name,description,names])=>({id,name,description,nodeIds:names.map(n=>S+n+'.cs')}));
out.push({id:'layer:package',name:'Package Definition & Documentation',
 description:'The asmdef that compiles these scripts into the LeanTouchPlus assembly, the UPM manifest declaring the LeanCommon/LeanTouch dependencies, and the bundled HTML manual cataloguing every demo scene - the non-code shell that makes the component library installable and discoverable.',
 nodeIds:extra});

const nodes=require(process.cwd()+'/.ua/tmp/p4-filenodes.json');
const all=(Array.isArray(nodes)?nodes:(nodes.fileNodes||nodes.nodes)).map(n=>n.id);
const assigned=out.flatMap(l=>l.nodeIds);
const dupes=assigned.filter((x,i)=>assigned.indexOf(x)!==i);
const missing=all.filter(x=>!assigned.includes(x));
const bogus=assigned.filter(x=>!all.includes(x));
console.log('layers',out.length,'assigned',assigned.length,'input',all.length);
console.log('dupes',dupes,'missing',missing,'bogus',bogus);
if(dupes.length||missing.length||bogus.length||assigned.length!==all.length) process.exit(1);
fs.mkdirSync('.ua/intermediate',{recursive:true});
fs.writeFileSync('.ua/intermediate/layers.json',JSON.stringify(out,null,2));
console.log('OK ->', out.map(l=>l.id+':'+l.nodeIds.length).join(' '));
