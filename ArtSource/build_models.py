"""Build the four original models, their rigs and their animation takes with Blender 5.x.

blender --background --factory-startup --python ArtSource/build_models.py
All geometry is described below in metres. Animals face -Y and stand on Z=0.
"""
from pathlib import Path
import math
import random

import bpy
from mathutils import Vector

SOURCE = Path(__file__).resolve().parent
EXPORT = SOURCE.parent / "Assets" / "Models" / "MeadowArt"
TAU = 2 * math.pi


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.context.scene.render.fps = 24
    bpy.context.scene.unit_settings.system = 'METRIC'


def material(name, hex_color):
    def linear(i):
        c = int(hex_color[i:i + 2], 16) / 255
        return c / 12.92 if c <= .04045 else ((c + .055) / 1.055) ** 2.4
    rgb = [linear(i) for i in (0, 2, 4)] + [1]
    m = bpy.data.materials.new(name)
    m.diffuse_color = rgb
    m.use_nodes = True
    node = m.node_tree.nodes.get("Principled BSDF")
    node.inputs["Base Color"].default_value = rgb
    node.inputs["Roughness"].default_value = .87
    return m


class Mesh:
    """A single skinned mesh per actor, with flat facets and named material slots."""
    def __init__(self, palette):
        self.verts, self.faces, self.face_materials, self.weights = [], [], [], {}
        self.names = list(palette)
        self.materials = [material(k, v) for k, v in palette.items()]

    def vertex(self, p, bone):
        i = len(self.verts)
        self.verts.append(tuple(p))
        if bone:
            self.weights.setdefault(bone, []).append(i)
        return i

    def face(self, indices, mat):
        self.faces.append(tuple(indices))
        self.face_materials.append(self.names.index(mat))

    def join_rings(self, rings, mat, ends=True):
        n = len(rings[0])
        for j, (a, b) in enumerate(zip(rings, rings[1:])):
            for i in range(n):
                c = mat(j, i, n) if callable(mat) else mat
                self.face((a[i], a[(i + 1) % n], b[(i + 1) % n], b[i]), c)
        if ends:
            c = mat(0, 0, n) if callable(mat) else mat
            self.face(reversed(rings[0]), c)
            self.face(rings[-1], c)

    def loft(self, sections, mat, bone, sides=10):
        # The sections describe the profile from rear to nose or from tip to root.
        rings = []
        for y, z, rx, rz in sections:
            rings.append([self.vertex((rx * math.cos(TAU*i/sides), y, z + rz * math.sin(TAU*i/sides)), bone)
                          for i in range(sides)])
        self.join_rings(rings, mat)

    def oval(self, c, scale, mat, bone, sides=9, latitudes=5, swivel=0):
        rings = []
        for j in range(1, latitudes):
            lat = math.pi * j / latitudes - math.pi / 2
            ring = []
            for i in range(sides):
                a = TAU * i / sides + swivel
                ring.append(self.vertex((c[0]+scale[0]*math.cos(lat)*math.cos(a),
                                         c[1]+scale[1]*math.cos(lat)*math.sin(a), c[2]+scale[2]*math.sin(lat)), bone))
            rings.append(ring)
        self.join_rings(rings, mat, False)
        lo = self.vertex((c[0], c[1], c[2]-scale[2]), bone)
        hi = self.vertex((c[0], c[1], c[2]+scale[2]), bone)
        for i in range(sides):
            self.face((lo, rings[0][(i+1)%sides], rings[0][i]), mat)
            self.face((hi, rings[-1][i], rings[-1][(i+1)%sides]), mat)

    def tube(self, sections, mat, bone, sides=7):
        # Sections contain centre, horizontal radius and sagittal radius.
        tangent = (Vector(sections[-1][0]) - Vector(sections[0][0])).normalized()
        guide = Vector((1,0,0)) if abs(tangent.x) < .85 else Vector((0,1,0))
        u = (guide - tangent * tangent.dot(guide)).normalized()
        v = tangent.cross(u).normalized()
        rings = []
        for p, r, h in sections:
            rings.append([self.vertex(Vector(p) + u*r*math.cos(TAU*i/sides) + v*h*math.sin(TAU*i/sides), bone)
                          for i in range(sides)])
        self.join_rings(rings, mat)

    def object(self, name, rig=None):
        data = bpy.data.meshes.new(name)
        data.from_pydata(self.verts, [], self.faces)
        data.update()
        obj = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(obj)
        for m in self.materials: data.materials.append(m)
        for poly, index in zip(data.polygons, self.face_materials): poly.material_index = index
        # Closed components can have different construction axes. Make their normals consistent.
        import bmesh
        bm = bmesh.new()
        bm.from_mesh(data)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        bm.to_mesh(data)
        bm.free()
        if rig:
            for bone, indices in self.weights.items():
                obj.vertex_groups.new(name=bone).add(indices, 1, 'REPLACE')
            obj.parent = rig
            mod = obj.modifiers.new("Skeleton", 'ARMATURE')
            mod.object = rig
        return obj


def skeleton(name, definitions):
    arm = bpy.data.armatures.new(name)
    rig = bpy.data.objects.new(name + " Rig", arm)
    bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    for label, head, tail, parent in definitions:
        b = arm.edit_bones.new(label)
        b.head, b.tail = head, tail
        if parent: b.parent = arm.edit_bones[parent]
    bpy.ops.object.mode_set(mode='OBJECT')
    arm.display_type = 'STICK'
    rig.show_in_front = True
    return rig


def animal_rig(is_wolf):
    if is_wolf:
        hip, neck, nose = (0,.48,.81), (0,-.42,.96), (0,-.9,1.01)
        legs = (.18,-.42,.48,.72,.33)
    else:
        hip, neck, nose = (0,.40,.71), (0,-.43,.82), (0,-.76,.76)
        legs = (.19,-.35,.37,.60,.24)
    bones = [('Body',hip,neck,None), ('Head',neck,nose,'Body'),
             ('Jaw',(0,nose[1]+.04,nose[2]-.06),(0,nose[1]-.17,nose[2]-.08),'Head'),
             ('Tail',(0,hip[1]+.03,hip[2]+.06),(0,hip[1]+.29,hip[2]+.02),'Body')]
    x, front, back, top, knee = legs
    for side, sign in [('L',1),('R',-1)]:
        xside = sign*x
        ear = (sign*.18, -.65, 1.11 if is_wolf else .90)
        tip = (sign*(.25 if is_wolf else .43), -.61, 1.40 if is_wolf else .83)
        bones.append(('Ear.'+side,ear,tip,'Head'))
        for limb,y in [('Front',front),('Hind',back)]:
            upper = limb+'.'+side
            joint = (xside,y + (.065 if limb=='Hind' else .015),knee)
            bones.extend([(upper,(xside,y,top),joint,'Body'),
                          ('Shin.'+upper,joint,(xside,y-.035,.05),upper)])
    return skeleton('Wolf' if is_wolf else 'Sheep',bones), legs


def add_legs(m, parts, wolf):
    x, front, back, top, knee = parts
    for sign, side in [(1,'L'),(-1,'R')]:
        for limb,y in [('Front',front),('Hind',back)]:
            tag = limb+'.'+side
            x0 = sign*x
            jy = y + (.065 if limb=='Hind' else .015)
            wide = .112 if wolf and limb=='Hind' else .092
            m.tube([((x0,y,top+.08),wide,wide*1.3),((x0,(y+jy)/2,(top+knee)/2),wide*.9,wide),
                    ((x0,jy,knee-.025),.054,.061)], 'Coat' if wolf else 'Face',tag)
            m.tube([((x0,jy,knee+.035),.055,.064),((x0,jy+.018,.14),.041,.049),
                    ((x0,y-.025,.068),.045,.055)], 'Leg' if wolf else 'Face','Shin.'+tag)
            foot = 'Shin.'+tag
            if wolf:
                m.oval((x0,y-.060,.074),(.072,.124,.058),'Leg',foot,9,5)
                for dx in (-.041,0,.041):
                    m.oval((x0+dx,y-.14,.049),(.031,.055,.035),'Soot',foot,7,4)
            else:
                m.oval((x0,y-.041,.071),(.057,.079,.056),'Soot',foot,8,4)
                for dx in (-.025,.025):
                    m.oval((x0+dx,y-.084,.041),(.030,.055,.035),'Soot',foot,7,4)


def sheep():
    reset()
    rig, legs = animal_rig(False)
    m = Mesh(dict(Fleece='F1E6CE',Cream='FFF1D8',Oat='DFCDA8',Face='524842',Soot='272A2A',
                  Inner='A27661',Eye='161D21',Glint='FFF8DC'))
    m.loft([(.55,.72,.09,.16),(.48,.73,.25,.28),(.29,.75,.35,.33),(0,.78,.38,.36),
            (-.29,.80,.34,.32),(-.48,.80,.24,.25),(-.52,.81,.12,.16)],'Fleece','Body',12)
    # Broad wool scallops follow the barrel. A smaller fringe hangs over the forehead.
    for s in (-1,1):
        for j,(y,z) in enumerate([(.34,.83),(.13,.87),(-.10,.88),(-.32,.87)]):
            m.oval((s*(.234 if j in (0,3) else .277),y,z),(.12,.145,.134),
                   'Fleece' if j%2 else 'Cream','Body',8,5,j*.19)
    for j,y in enumerate([.32,.08,-.15,-.32]):
        m.oval(((-1)**j*.05,y,1.01),(.19,.145,.106),'Cream','Body',9,5,.16*j)
    m.loft([(-.40,.84,.13,.13),(-.53,.86,.19,.18),(-.67,.85,.205,.19),
            (-.81,.802,.16,.145),(-.90,.75,.11,.085)],'Face','Head',10)
    m.oval((0,-.88,.728),(.105,.079,.062),'Soot','Jaw',9,4)
    for side,s in [('L',1),('R',-1)]:
        m.tube([((s*.12,-.58,.91),.079,.060),((s*.27,-.61,.90),.103,.062),
                ((s*.43,-.60,.81),.034,.021)],'Face','Ear.'+side,7)
        m.oval((s*.285,-.661,.895),(.088,.037,.033),'Inner','Ear.'+side,8,4)
        m.oval((s*.168,-.729,.87),(.041,.039,.047),'Eye','Head',9,5)
        m.oval((s*.199,-.746,.886),(.012,.012,.013),'Glint','Head',7,4)
        m.oval((s*.111,-.666,1.005),(.126,.105,.086),'Cream','Head',9,5)
    m.oval((0,-.70,1.018),(.14,.109,.09),'Fleece','Head',8,5,.2)
    m.oval((0,.552,.76),(.115,.151,.124),'Oat','Tail',8,5)
    add_legs(m, legs, False)
    obj=m.object('Sheep | fleece, face and hooves',rig)
    animate_animal(rig,False)
    save('Sheep',rig,obj)


def wolf():
    reset()
    rig, legs = animal_rig(True)
    m = Mesh(dict(Coat='677886',Mantle='43535F',Leg='585D5C',Silver='A4B3AE',
                  Cream='DCD7BB',Soot='282F32',Inner='956F63',Amber='EEC46A',Glint='FFEDC0'))
    def coat(j,i,n):
        h=math.sin(TAU*(i+.5)/n)
        return 'Mantle' if h>.64 else 'Silver' if h<-.65 else 'Coat'
    m.loft([(.61,.82,.09,.145),(.51,.82,.207,.22),(.28,.86,.21,.235),(.02,.88,.173,.188),
            (-.22,.90,.21,.255),(-.41,.94,.234,.284),(-.53,1.0,.155,.217)],coat,'Body',10)
    m.loft([(-.40,.999,.139,.16),(-.54,1.043,.19,.218),(-.68,1.06,.223,.205),
            (-.79,1.045,.17,.137),(-.91,1.00,.133,.09),(-1.035,1.0,.092,.056)],coat,'Head',10)
    # Chin, muzzle, tapered cheek ruffs and the dark dorsal saddle mark the silhouette.
    m.oval((0,-.92,.954),(.117,.17,.059),'Cream','Jaw',9,4)
    m.oval((0,-1.032,1.012),(.094,.059,.058),'Soot','Head',8,4)
    for side,s in [('L',1),('R',-1)]:
        m.tube([((s*.129,-.64,1.17),.100,.077),((s*.180,-.60,1.305),.066,.057),
                ((s*.234,-.575,1.418),.009,.008)],'Mantle','Ear.'+side,6)
        # Front-facing inset triangle.
        pts=[m.vertex((s*x,y,z),'Ear.'+side) for x,y,z in [(.112,-.701,1.201),(.196,-.646,1.347),(.210,-.665,1.225)]]
        m.face(pts if s>0 else list(reversed(pts)), 'Inner')
        m.tube([((s*.145,-.655,1.027),.078,.073),((s*.210,-.588,.983),.069,.059),
                ((s*.278,-.498,.91),.008,.009)],'Silver','Head',7)
        m.oval((s*.17,-.774,1.095),(.040,.058,.040),'Soot','Head',8,5)
        m.oval((s*.201,-.788,1.10),(.016,.032,.021),'Amber','Head',8,4)
        m.oval((s*.214,-.800,1.104),(.007,.013,.011),'Soot','Head',7,4)
    m.tube([((0,.47,.89),.09,.11),((0,.69,.83),.115,.144),
            ((0,.88,.695),.129,.151),((0,1.04,.66),.091,.095)],'Mantle','Tail',9)
    m.tube([((0,.985,.67),.101,.105),((0,1.106,.666),.063,.074),
            ((0,1.187,.694),.009,.015)],'Cream','Tail',8)
    add_legs(m,legs,True)
    obj=m.object('Wolf | saddle, muzzle and brush',rig)
    animate_animal(rig,True)
    save('Wolf',rig,obj)


def rotate(rig, bone, frame, pitch=0, sway=0, roll=0, shift=None, scale=None):
    from mathutils import Euler
    p = rig.pose.bones[bone]
    basis = p.bone.matrix_local.to_quaternion()
    rotation = Euler(tuple(map(math.radians,(pitch,roll,sway))),'XYZ').to_quaternion()
    p.rotation_mode = 'QUATERNION'
    p.rotation_quaternion = basis.inverted() @ rotation @ basis
    p.keyframe_insert(data_path="rotation_quaternion", frame=frame, group=bone)
    if shift is not None:
        p.location = basis.inverted() @ Vector(shift)
        p.keyframe_insert(data_path="location", frame=frame, group=bone)
    if scale is not None:
        p.scale = (scale,scale,scale)
        p.keyframe_insert(data_path="scale", frame=frame, group=bone)


def take(rig, label, length, start, pose):
    animation = rig.animation_data_create()
    for track in animation.nla_tracks: track.mute = True
    for bone in rig.pose.bones:
        bone.matrix_basis.identity()
    action = bpy.data.actions.new(label)
    animation.action = action
    for i in range(13):
        pose(1 + length*i/12, i/12)
    animation.action = None
    track = animation.nla_tracks.new()
    track.name = label
    strip = track.strips.new(label,start,action)
    strip.name = label
    strip.extrapolation = 'NOTHING'
    strip.blend_type = 'REPLACE'
    for t in animation.nla_tracks: t.mute = False


def animate_animal(rig, wolf):
    for name, duration, start in [('Walk',27,1),('Run',16,40),('Eat',43,70)]:
        def pose(frame, t):
            wave = math.sin(TAU*t)
            bounce = (1-math.cos(TAU*2*t))*.5
            run = name == 'Run'
            eating = name == 'Eat'
            if eating:
                nod = (.5-.5*math.cos(TAU*t))
                rotate(rig,'Body',frame,pitch=5+3*nod,shift=(0,0,-.018-.021*nod))
                rotate(rig,'Head',frame,pitch=(29 if wolf else 36)+12*nod,sway=2*wave)
                rotate(rig,'Jaw',frame,pitch=7+8*math.sin(TAU*3*t))
            else:
                rise = (.075 if wolf else .112) if run else (.027 if wolf else .04)
                rotate(rig,'Body',frame,pitch=(8 if run else 2)*wave,roll=(1.4 if not run else .6)*wave,
                       shift=(0,0,rise*bounce))
                rotate(rig,'Head',frame,pitch=(-6 if run else -2)*wave,shift=(0,0,.012*bounce))
                rotate(rig,'Jaw',frame,pitch=(2 if run else 0)+(1.5 if run else 0)*wave)
            rotate(rig,'Tail',frame,pitch=(9 if run else 4)*wave,sway=(13 if wolf else 9)*math.sin(TAU*t+.45))
            for sign,side in [(1,'L'),(-1,'R')]:
                rotate(rig,'Ear.'+side,frame,pitch=(5 if run else 2)*math.sin(TAU*t-.35),
                       roll=sign*(8 if run and not wolf else 3)*math.sin(TAU*2*t+.25))
                for limb in ('Front','Hind'):
                    if run:
                        phase = (0 if limb=='Front' else .47) + (0 if sign==1 else (.16 if wolf else .085))
                    else:
                        phase = (0 if limb=='Front' else .5) + (0 if sign==1 else .5)
                    swing = math.sin(TAU*(t+phase))
                    fold = max(0,math.cos(TAU*(t+phase)))
                    leg = limb+'.'+side
                    angle = (33 if run else 20)*swing
                    bend = (42 if run else 23)*fold * (-1 if limb=='Front' else 1)
                    if eating:
                        angle = (-7 if limb=='Front' else 5) + 1.4*wave
                        bend = 5 if limb=='Front' else -4
                    rotate(rig,leg,frame,pitch=angle)
                    rotate(rig,'Shin.'+leg,frame,pitch=bend)
        take(rig,name,duration,start,pose)
    bpy.context.scene.frame_end = 113
    bpy.context.scene.frame_set(1)


def grass():
    reset()
    bones=[('Crown',(0,0,0),(0,0,.17),None)]
    for k in range(3):
        a=TAU*k/3
        x,y=.09*math.cos(a),.09*math.sin(a)
        bones.append(('Sprig'+str(k),(x,y,.13),(1.35*x,1.35*y,.49),'Crown'))
    rig=skeleton('Grass',bones)
    m=Mesh(dict(Leaf='458451',Sun='87B45C',Shade='346C4B',Lime='A6BE68',Straw='C2AD65'))
    rng=random.Random(44)
    for i in range(25):
        a=i*2.39996+rng.uniform(-.19,.19)
        out=Vector((math.cos(a),math.sin(a),0))
        side=Vector((-out.y,out.x,0))
        r=rng.uniform(.03,.26)
        base=out*r
        height=(.36+.25*(1-r/.28))*rng.uniform(.75,1.15)
        reach=rng.uniform(.12,.31)
        width=rng.uniform(.031,.067)
        sprig='Sprig'+str(round((a%TAU)/(TAU/3))%3)
        rings=[]
        for t,w in [(0,.34),(.34,1),(.68,.63),(.96,.1)]:
            c=base+out*reach*t*t+Vector((0,0,height*t))
            g='Crown' if t==0 else sprig
            rings.append([m.vertex(c-side*w*width,g),m.vertex(c+out*w*width*.41,g),
                          m.vertex(c+side*w*width,g)])
        shades=[('Leaf','Sun'),('Shade','Leaf'),('Sun','Lime')][i%3]
        m.join_rings(rings,lambda j,k,n:shades[0 if k==0 else 1])
    for i in range(3):
        a=TAU*i/3+.3
        p=Vector((.07*math.cos(a),.07*math.sin(a),0))
        top=p+Vector((.12*math.cos(a),.12*math.sin(a),.56+i*.035))
        m.tube([(p,.009,.009),(top,.005,.005)],'Leaf','Sprig'+str(i),5)
        m.oval(top,(.021,.019,.062),'Straw','Sprig'+str(i),6,4)
    obj=m.object('Grass | folded blades and seed heads',rig)
    def breeze(frame,t):
        rotate(rig,'Crown',frame,pitch=2*math.sin(TAU*t))
        for i in range(3):
            rotate(rig,'Sprig'+str(i),frame,pitch=7*math.sin(TAU*t+i),roll=4*math.cos(TAU*t+i))
    def unfurl(frame,t):
        smooth=t*t*(3-2*t)
        rotate(rig,'Crown',frame,scale=.055+.945*smooth)
        for i in range(3):
            rotate(rig,'Sprig'+str(i),frame,pitch=(1-smooth)*(15-10*i),roll=(1-smooth)*(i-1)*13)
    take(rig,'Sway',54,1,breeze)
    take(rig,'Grow',36,70,unfurl)
    bpy.context.scene.frame_end=106
    bpy.context.scene.frame_set(1)
    save('Grass',rig,obj)


def ground():
    reset()
    m=Mesh(dict(Meadow='91A976',Sage='95AE7A',Fern='8DA575',Light='9AB27F',
                Rim='799367',Clay='A78566',Earth='806A57',Stone='B5AE93'))
    # An irregular grass plate with rounded corners and a cutaway soil profile.
    n=16
    grid={}
    for j in range(n+1):
        for i in range(n+1):
            x,y=-32+i*4,-32+j*4
            dx,dy=max(0,abs(x)-29),max(0,abs(y)-29)
            if dx*dx+dy*dy>9:
                size=math.hypot(dx,dy)
                x=math.copysign(29+3*dx/size,x)
                y=math.copysign(29+3*dy/size,y)
            grid[i,j]=m.vertex((x,y,.014*math.sin(i*.91)*math.sin(j*.73)),None)
    for j in range(n):
        for i in range(n):
            a,b,c,d=grid[i,j],grid[i+1,j],grid[i+1,j+1],grid[i,j+1]
            v=math.sin(i*.53+j*.22)+math.sin(j*.70-i*.31)
            color='Fern' if v<-.82 else 'Light' if v>1.22 else 'Sage' if v>.12 else 'Meadow'
            m.face((a,b,c,d),color)
    border=[grid[i,0] for i in range(n)]+[grid[n,j] for j in range(n)]+[grid[i,n] for i in range(n,0,-1)]+[grid[0,j] for j in range(n,0,-1)]
    rings=[border]
    for dep,shrink in [(-.20,1.0),(-.52,.997),(-1.08,.984),(-1.26,.972)]:
        rings.append([m.vertex((m.verts[k][0]*shrink,m.verts[k][1]*shrink,dep),None) for k in border])
    for top,low,col in zip(rings,rings[1:],['Rim','Clay','Clay','Earth']):
        for i in range(len(top)): m.face((top[i],top[(i+1)%len(top)],low[(i+1)%len(top)],low[i]),col)
    m.face(reversed(rings[-1]),'Earth')
    obj=m.object('Meadow | grass cap and earthen banks')
    save('MeadowGround',None,obj)


def save(name,rig,obj):
    scene=bpy.context.scene
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    if rig: rig.select_set(True)
    bpy.context.view_layer.objects.active=rig or obj
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                space=area.spaces.active
                space.shading.color_type='MATERIAL'
                space.overlay.show_overlays=False
                region=space.region_3d
                region.view_rotation=Vector((2.2,-3.4,1.45)).to_track_quat('Z','Y')
                region.view_distance=3.5 if name in ('Wolf','Sheep') else 1.65 if rig else 93
                region.view_location=(0,0,.64 if name in ('Wolf','Sheep') else .26 if rig else 0)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(name+'.blend')),compress=True)
    bpy.ops.export_scene.fbx(filepath=str(EXPORT/(name+'.fbx')),use_selection=True,
        object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS',add_leaf_bones=False,bake_anim=rig is not None,
        bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,bake_anim_simplify_factor=0,
        use_custom_props=False, mesh_smooth_type='FACE')
    print('MEADOW_EXPORT',name,len(obj.data.vertices),'vertices',len(obj.data.polygons),'faces')


if __name__=='__main__':
    EXPORT.mkdir(parents=True,exist_ok=True)
    sheep()
    wolf()
    grass()
    ground()
