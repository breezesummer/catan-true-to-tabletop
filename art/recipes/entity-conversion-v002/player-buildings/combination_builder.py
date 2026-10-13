"""Actual separate body and ring meshes, centred together at bottom origin."""
from copy import deepcopy
import importlib.util
from pathlib import Path


def load_builder(name):
    path=Path(__file__).resolve().with_name(name+'_builder.py')
    spec=importlib.util.spec_from_file_location('v002_player_building_'+name,path)
    module=importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def build(asset, helpers):
    kind=asset['builderArgs']['bodyKind']
    if kind not in ('city','metropolis'):
        raise ValueError('Combination requires a city or metropolis body')
    body=deepcopy(asset)
    body['id']=asset['id']+'-body'
    body['dimensionsMm']=[18,18,20 if kind=='city' else 24]
    wall=deepcopy(asset)
    wall['id']=asset['id']+'-wall'
    wall['dimensionsMm']=[24,24,5]
    wall['builderArgs']['innerDiameterMm']=18.6
    objects=load_builder(kind).build(body,helpers)
    for obj in objects:
        obj.name='Combined_'+kind.title()+'_Body'
        obj.data.name=obj.name+'_Mesh'
    walls=load_builder('wall').build(wall,helpers)
    for obj in walls:
        obj.name='Combined_Independent_City_Wall'
        obj.data.name=obj.name+'_Mesh'
    return objects+walls
