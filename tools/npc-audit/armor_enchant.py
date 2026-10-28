"""Read-only source selection for one armor enchant comparison."""
from pathlib import Path
import xml.etree.ElementTree as ET


DEFAULT_ARMOR_RANGES = {
    'RB_TORSO': 0xf97001e0, 'LT_TORSO': 0xf96cf4a0, 'CH_TORSO': 0xf969e760, 'PL_TORSO': 0xf96860c0,
    'RB_GLOVE': 0xf960bfa0, 'LT_GLOVE': 0xf95db260, 'CH_GLOVE': 0xf95aa520, 'PL_GLOVE': 0xf9591e80,
    'RB_SHOULDER': 0xf9517d60, 'LT_SHOULDER': 0xf94e7020, 'CH_SHOULDER': 0xf94b62e0, 'PL_SHOULDER': 0xf949dc40,
    'RB_PANTS': 0xf9423b20, 'LT_PANTS': 0xf93f2de0, 'CH_PANTS': 0xf93c20a0, 'PL_PANTS': 0xf93a9a00,
    'RB_SHOES': 0xf932f8e0, 'LT_SHOES': 0xf92feba0, 'CH_SHOES': 0xf92cde60, 'PL_SHOES': 0xf92b57c0,
    'SHIELD': 0xf9253d40,
}


def default_retail_table(item_id):
    matches = [group for group, constant in DEFAULT_ARMOR_RANGES.items()
               if ((item_id + constant) & 0xffffffff) < 100000]
    if len(matches) != 1:
        raise ValueError('Item ID has no unique reviewed retail armor default')
    group = matches[0].lower()
    return group.replace('_glove', '_gloves') if group.endswith('_glove') else group


def selected_region_file(root, filename):
    root = Path(root)
    regional = root / 'USA' / filename
    selected = regional if regional.is_file() else root / filename
    if not selected.is_file():
        raise ValueError('Missing selected retail file: ' + str(selected))
    return selected, 'USA override' if selected == regional else 'USA missing; base fallback'


def one_item(path, tag, item_id, id_attribute=False):
    """Stream every record to detect duplicate IDs without retaining the whole file."""
    found = None
    stack = []
    for event, node in ET.iterparse(path, events=('start', 'end')):
        if event == 'start':
            stack.append(node)
            continue
        if node.tag == tag:
            identifier = node.get('id') if id_attribute else node.findtext('id')
            if identifier == str(item_id):
                if not id_attribute and len(node.findall('id')) != 1:
                    raise ValueError('Ambiguous retail item ID in ' + str(path))
                if found is not None:
                    raise ValueError('Duplicate item ID in ' + str(path))
                found = ET.fromstring(ET.tostring(node))
            if len(stack) > 1:
                stack[-2].remove(node)
            node.clear()
        stack.pop()
    if found is None:
        raise ValueError(f'Item {item_id} not found in {path}')
    return found


def unique_by(root, tag, name):
    matches = [x for x in root if x.tag == tag and name(x)]
    if len(matches) != 1:
        raise ValueError('Selected enchant table is missing or ambiguous')
    return matches[0]


def integer(text, label):
    if text is None:
        raise ValueError('Missing ' + label)
    try:
        value = int(text)
    except ValueError as error:
        raise ValueError('Invalid ' + label) from error
    if not -(2**31) <= value < 2**31:
        raise ValueError('Out-of-range ' + label)
    return value


def unique_text(node, tag, required=False):
    matches = node.findall(tag)
    if len(matches) > 1 or required and len(matches) != 1:
        raise ValueError('Missing or duplicate ' + tag)
    if matches and (len(matches[0]) or matches[0].attrib):
        raise ValueError('Unreviewed structure for ' + tag)
    return matches[0].text if matches else None


def retail_level(node, level, attribute='physical_defend'):
    matches = [x for x in node.findall('./enchant_attr_list/data') if x.findtext('level') == str(level)]
    if len(matches) != 1:
        raise ValueError('Missing or ambiguous retail enchant level ' + str(level))
    unique_text(matches[0], 'level', required=True)
    values = []
    for child in matches[0]:
        if child.tag.startswith('attr') and child.text:
            fields = child.text.split()
            if fields and fields[0].casefold() == attribute:
                if len(fields) != 2:
                    raise ValueError('Unreviewed retail enchant attribute')
                values.append(integer(fields[1], 'retail enchant attribute'))
    if len(values) > 1:
        raise ValueError('Duplicate retail enchant attribute')
    return values[0] if values else 0


def host_level(node, level, attribute='PHYSICAL_DEFENSE'):
    matches = [x for x in node.findall('./enchant_data') if x.get('level') == str(level)]
    if len(matches) != 1:
        raise ValueError('Missing or ambiguous host enchant level ' + str(level))
    values = [integer(x.get('value'), 'host enchant attribute') for x in matches[0]
              if x.tag == 'enchant_stat' and x.get('stat') == attribute]
    total = sum(values)
    if not -(2**31) <= total < 2**31:
        raise ValueError('Host enchant defense sum overflows')
    return total


def prepare(item_id, level, retail_root, host_items, host_tables, stat='physical-defense'):
    if not 0 < item_id < 2**31 or not 0 <= level <= 255:
        raise ValueError('Item ID or enchant level is outside the reviewed range')
    attributes = {'physical-defense': ('physical_defend', 'PHYSICAL_DEFENSE'), 'max-hp': ('max_hp', 'MAXHP')}
    if stat not in attributes:
        raise ValueError('Unsupported enchant attribute')
    retail_attribute, host_attribute = attributes[stat]
    retail_items, item_selection = selected_region_file(retail_root, 'item_armors.xml')
    retail_tables, table_selection = selected_region_file(retail_root, 'item_enchanttable.xml')
    host_items, host_tables = Path(host_items), Path(host_tables)
    retail_item = one_item(retail_items, 'item', item_id)
    host_item = one_item(host_items, 'item_template', item_id, True)
    retail_name, host_name = unique_text(retail_item, 'name', required=True), host_item.get('cName')
    if not retail_name or not host_name or retail_name.casefold() != host_name.casefold():
        raise ValueError('4.8 and 5.8 item names do not agree for this ID')
    retail_override = unique_text(retail_item, 'enchant_name')
    if retail_item.find('enchant_name') is not None and retail_override is None:
        raise ValueError('Empty explicit retail enchant table name')
    native_name = retail_override if retail_override is not None else default_retail_table(item_id)
    host_override = host_item.get('enchant_name')
    host_name = host_override if host_override is not None else host_item.get('item_group')
    if not native_name or not host_name:
        raise ValueError('Empty explicit enchant table name or missing host group')
    rt = unique_by(ET.parse(retail_tables).getroot(), 'item_enchanttable',
                   lambda n: n.findtext('name') == native_name)
    unique_text(rt, 'name', required=True)
    ht = unique_by(ET.parse(host_tables).getroot(), 'enchant_list',
                   lambda n: n.get('item_group') == host_name)
    start = integer(unique_text(rt, 'start_limitless', required=True), 'retail threshold')
    if start < 1:
        raise ValueError('Retail threshold requires unreviewed zero/negative branch')
    if len(rt.findall('limitless_attr_list')) != 1:
        raise ValueError('Retail enchant table has no unique limitless attribute list')
    increments = [integer(x.findtext('attr_value'), 'retail limitless attribute')
                  for x in rt.findall('./limitless_attr_list/data')
                  if (x.findtext('attr_name') or '').casefold() == retail_attribute]
    if len(increments) > 1:
        raise ValueError('Duplicate retail limitless attributes')
    native_before = retail_level(rt, start - 1, retail_attribute) if start > 1 and level >= start else 0
    native_direct = retail_level(rt, level, retail_attribute) if 0 < level < start else 0
    host_levels = [integer(x.get('level'), 'host enchant level') for x in ht.findall('./enchant_data')]
    if len(host_levels) != len(set(host_levels)) or not host_levels:
        raise ValueError('Host enchant levels are missing or duplicated')
    maximum = max(host_levels)
    if maximum < 2:
        raise ValueError('Host enchant table is too short')
    host_before = host_level(ht, maximum - 1, host_attribute) if level >= maximum else 0
    host_increment = host_level(ht, maximum, host_attribute) if level >= maximum else 0
    host_direct = host_level(ht, level, host_attribute) if 0 < level < maximum else 0
    return {
        'item_id': item_id, 'item_name': retail_name, 'level': level, 'stat': stat,
        'retail_declared_max_enchant': unique_text(retail_item, 'max_enchant_value'),
        'host_declared_max_enchant': host_item.get('max_enchant'),
        'retail_table': native_name, 'host_table': host_name,
        'retail_selection': 'explicit' if retail_override is not None else 'default item-ID range',
        'host_selection': 'explicit' if host_override is not None else 'item group',
        'retail_item_selection': item_selection, 'retail_table_selection': table_selection,
        'inputs': [level, start, native_before, increments[0] if increments else 0,
                   native_direct, maximum, host_before, host_increment, host_direct],
        'source_paths': [retail_items, retail_tables, host_items, host_tables],
    }
