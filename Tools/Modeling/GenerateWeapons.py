import bpy, math, os, sys, json
from mathutils import Vector
OUT=sys.argv[sys.argv.index('--')+1]
os.makedirs(OUT,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
def mat(name,c,metal=0):
 m=bpy.data.materials.new(name);m.diffuse_color=(*c,1);m.use_nodes=True
 bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*c,1);bs.inputs['Metallic'].default_value=metal;bs.inputs['Roughness'].default_value=.65
 return m
steel=mat('Gunmetal',(.10,.115,.13),.55);edge=mat('Machined steel',(.24,.26,.28),.65);black=mat('Polymer',(.045,.05,.054));olive=mat('Olive stock',(.21,.25,.115));wood=mat('Walnut grips',(.24,.10,.045));dark=mat('Bore',(.009,.012,.015));glass=mat('Scope glass',(.035,.16,.19),.7)
def cv(p):return Vector((p[0],-p[2],p[1]))
parts=[]
def finish(o,n,m):
 o.name=n;o.data.materials.append(m);parts.append(o);return o
def box(n,p,s,m=steel,bevel=.002):
 bpy.ops.mesh.primitive_cube_add(size=1,location=cv(p));o=bpy.context.object;o.scale=(s[0],s[2],s[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if bevel:
  mod=o.modifiers.new('Machined edges','BEVEL');mod.width=bevel;mod.segments=1;bpy.ops.object.modifier_apply(modifier=mod.name)
 return finish(o,n,m)
def rod(n,a,b,r,m=steel,verts=10,r2=None):
 a,b=cv(a),cv(b);d=b-a
 bpy.ops.mesh.primitive_cone_add(vertices=verts,radius1=r,radius2=r if r2 is None else r2,depth=d.length,location=(a+b)/2)
 o=bpy.context.object;o.rotation_euler=d.to_track_quat('Z','Y').to_euler();return finish(o,n,m)
def grip(m=black):
 o=box('Pistol grip',(0,-.018,-.015),(.034,.105,.044),m,.006);o.rotation_euler.x=math.radians(-12)
 for y in [-.04,-.025,-.01]:box('Grip grooves',(0,y,-.04),(.036,.004,.004),edge,0)
 box('Trigger guard bottom',(0,-.018,.045),(.018,.009,.07),black)
 box('Trigger guard front',(0,.004,.079),(.018,.045,.01),black)
 rod('Trigger',(0,.026,.035),(0,-.005,.045),.004,edge,6)
def sight(z,y=.12):
 box('Sight post',(0,y,z),(.014,.022,.014),black)
 for x in [-.013,.013]:box('Sight ears',(x,y+.003,z),(.005,.023,.012),steel)
def barrel(z1,z2,y=.085,r=.012):
 rod('Barrel',(0,y,z1),(0,y,z2),r);rod('Muzzle crown',(0,y,z2-.024),(0,y,z2),r*1.3,edge)
 rod('Dark bore',(0,y,z2),(0,y,z2+.0006),r*.65,dark)
def mag(z,y=-.06,w=.04,h=.16):
 o=box('Magazine',(0,y,z),(w,h,.072),steel,.005);o.rotation_euler.x=-.13
 for x in [-w/2-.001,w/2+.001]:
  for dz in [-.018,.007,.028]:box('Magazine rib',(x,y,z+dz),(.003,h*.75,.004),edge,0)
def galil():
 grip();box('Receiver',(0,.07,.07),(.066,.075,.27));box('Dust cover',(0,.111,.055),(.055,.02,.23),edge)
 box('Polymer handguard',(0,.067,.29),(.064,.065,.20),black,.009)
 for z in [.22,.25,.28,.31,.34]:
  for x in [-.033,.033]:box('Cooling slit',(x,.083,z),(.003,.012,.016),dark,0)
 barrel(.38,.61)
 rod('Gas tube',(0,.118,.17),(0,.118,.46),.012);sight(.45,.14);sight(-.03,.14)
 box('Stock neck',(0,.055,-.14),(.035,.044,.12));box('Stock',(0,.015,-.275),(.052,.105,.18),black,.014);box('Butt pad',(0,.008,-.37),(.056,.13,.022),black)
 mag(.11);rod('Charging handle',(.027,.095,.03),(.065,.115,.03),.009,edge)
 return .61,.085

def famas():
 grip();box('Bullpup receiver',(0,.07,-.015),(.078,.10,.41),black,.012);box('Butt stock',(0,.023,-.20),(.081,.18,.11),black,.015)
 box('Stock pad',(0,.02,-.26),(.085,.18,.02),steel)
 mag(-.15,-.065,.042,.15)
 box('Foregrip',(0,.035,.19),(.067,.06,.14),black,.008)
 for z in [.15,.18,.21,.24]:box('Foregrip rib',(0,.018,z),(.071,.012,.006),steel,0)
 for z in [-.16,.19]:box('Carry handle support',(0,.155,z),(.029,.12,.035),black)
 box('Carry handle bridge',(0,.215,.012),(.043,.029,.40),black,.004);sight(.18,.235);sight(-.16,.235)
 barrel(.24,.49);box('Ejection port',(.040,.092,-.09),(.003,.029,.08),edge,0)
 for x in [-.043,.043]:rod('Folded bipod',(x,.086,.28),(x,.045,.075),.006,edge,8)
 return .49,.085

def awp():
 grip(olive);box('Chassis',(0,.045,.085),(.07,.068,.42),olive,.012);box('Receiver',(0,.099,.06),(.048,.042,.23),steel)
 box('Stock neck',(0,.014,-.145),(.048,.065,.12),olive,.01);box('Stock',(0,-.005,-.26),(.065,.12,.18),olive,.018);box('Cheek rest',(0,.064,-.24),(.066,.045,.17),olive,.012);box('Recoil pad',(0,-.012,-.36),(.068,.145,.025),black)
 box('Magazine',(0,-.021,.10),(.053,.088,.085),steel,.005)
 barrel(.28,.78,.10,.014)
 for z in [-.015,.13]:box('Scope mount',(0,.153,z),(.037,.06,.024),black)
 rod('Scope tube',(0,.19,-.10),(0,.19,.24),.022,black,12)
 rod('Scope objective',(0,.19,.17),(0,.19,.27),.039,black,12,r2=.045);rod('Objective lens',(0,.19,.271),(0,.19,.272),.038,glass,12)
 rod('Eyepiece',(0,.19,-.14),(0,.19,-.06),.031,black,12);rod('Rear lens',(0,.19,-.141),(0,.19,-.142),.025,glass,12)
 rod('Elevation dial',(0,.21,.06),(0,.25,.06),.023,edge,12);rod('Windage dial',(.015,.19,.06),(.044,.19,.06),.020,edge)
 rod('Bolt handle',(.02,.095,-.015),(.065,.065,-.045),.008,edge);bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=.017,location=cv((.065,.065,-.045)));finish(bpy.context.object,'Bolt knob',black)
 for x in [-.035,.035]:rod('Folded bipod',(x,.012,.25),(x,-.003,.43),.008,black)
 return .78,.10

def tec():
 grip();rod('Tubular receiver',(0,.06,-.055),(0,.06,.19),.026,steel,12)
 barrel(.18,.25,.06,.012);rod('Ventilated barrel jacket',(0,.06,.115),(0,.06,.215),.023,black,12)
 for z in [.13,.156,.182,.205]:
  for x in [-.023,.023]:box('Shroud vent',(x,.06,z),(.002,.014,.013),dark,0)
 box('Magazine',(0,-.122,-.018),(.029,.13,.038),steel,.003)
 for z in [-.034,-.005]:box('Magazine seam',(.016,-.122,z),(.002,.10,.004),edge,0)
 sight(-.03,.094);sight(.19,.094);rod('Charging knob',(-.02,.07,.025),(-.048,.075,.025),.009,edge)
 return .25,.06

def pistol(beretta=False):
 grip(wood if beretta else black)
 box('Frame',(0,.025,.052),(.038,.027,.22),steel if beretta else black,.005)
 if beretta:
  box('Rear slide',(0,.061,-.012),(.037,.032,.077),edge,.004)
  for x in [-.017,.017]:box('Open slide rail',(x,.057,.078),(.009,.024,.12),edge,.002)
  box('Slide nose',(0,.061,.148),(.038,.032,.025),edge,.003);barrel(.018,.174,.065,.009)
  rod('Hammer',(0,.059,-.052),(0,.083,-.059),.008,steel,6)
 else:
  box('Angular slide',(0,.065,.051),(.035,.037,.21),steel,.005);barrel(.145,.16,.063,.009)
  box('Accessory rail',(0,.012,.11),(.029,.012,.060),black)
 for z in [-.038,-.029,-.020,-.011]:
  for x in [-.019,.019]:box('Slide serration',(x,.065,z),(.002,.022,.003),black,0)
 sight(-.032,.087);box('Front sight',(0,.087,.14),(.007,.012,.008),black)
 box('Slide release',(.022,.038,.003),(.006,.008,.027),edge)
 return (.175 if beretta else .16),.065

recipes={'galil_ar':galil,'tec_9':tec,'five_seven':pistol,'dual_berettas':lambda:pistol(True),'awp':awp,'famas':famas}
report={}
for name,fn in recipes.items():
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False);parts=[]
 muzzle,y=fn()
 bpy.ops.object.select_all(action='DESELECT')
 for o in parts:o.select_set(True)
 bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();mesh=bpy.context.object;mesh.name='Body';bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
 root=bpy.data.objects.new('weapon_'+name,None);bpy.context.collection.objects.link(root);mesh.parent=root
 for nm,p in [('Grip',(0,0,0)),('Muzzle',(0,y,muzzle))]:
  o=bpy.data.objects.new(nm,None);bpy.context.collection.objects.link(o);o.parent=root;o.location=cv(p)
 mesh.data.calc_loop_triangles();report[name]={'triangles':len(mesh.data.loop_triangles),'length_m':round(mesh.dimensions.y,3),'note':'Dual Berettas mesh is one pistol; runtime equips one in each hand.' if name=='dual_berettas' else ''}
 bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'weapon_'+name+'.fbx'),axis_forward='-Z',axis_up='Y',object_types={'MESH','EMPTY'},use_triangles=True,bake_anim=False,add_leaf_bones=False)
 bpy.ops.export_scene.gltf(filepath=os.path.join(OUT,'weapon_'+name+'.glb'),export_format='GLB')
 # Render isometric profile with a consistent orthographic camera, neutral studio backdrop.
 scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.render.resolution_x=960;scene.render.resolution_y=600;scene.render.resolution_percentage=100
 scene.world=bpy.data.worlds.new('Studio');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.19,.22,.27,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.65
 center=mesh.matrix_world@ (sum((Vector(v) for v in mesh.bound_box),Vector())/8)
 bpy.ops.object.camera_add(location=center+Vector((1.1,1.4,.75)));cam=bpy.context.object;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=max(mesh.dimensions)*(1.9 if name in ('tec_9','five_seven','dual_berettas') else 1.3);scene.camera=cam
 for pos,power,size in [((1,0,2),110,2),((-1,-1,1),90,1.5)]:
  bpy.ops.object.light_add(type='AREA',location=center+Vector(pos));o=bpy.context.object;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(center-o.location).to_track_quat('-Z','Y').to_euler()
 scene.render.filepath=os.path.join(OUT,name+'.png');bpy.ops.render.render(write_still=True)
with open(os.path.join(OUT,'model-report.json'),'w') as f:json.dump(report,f,indent=2)


