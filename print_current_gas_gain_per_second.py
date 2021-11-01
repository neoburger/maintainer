from gevent import monkey
monkey.patch_all()
import gevent

from typing import Dict, List, Tuple

from decimal import Decimal
import json
import base64
from retry import retry

import requests

RequestExceptions = (
    requests.RequestException,
    requests.ConnectionError,
    requests.HTTPError,
    requests.Timeout,
)

url = 'https://neofura.ngd.network:1927'
NEO_scripthash = '0xef4073a0f2b305a38ec4050e4d3d28bc40ea63f5'
BNEO_scripthash = '0x48c40d4666f93408be1bef038b6722404d9a4c2a'
BNEO_address = 'NPmdLGJN47EddqYcxixdGMhtkr7Z5w4Aos'
total_neo = 100_000_000  # 100 million
GAS_per_block_from_holding_NEO = 0.5
GAS_per_block_from_voting = 4
seconds_per_block = 15
session = requests.Session()


@retry(RequestExceptions, tries=5, logger=None)
def fetch_candidates() -> Dict[str, int]:
    response = session.post(url, data=json.dumps(
        {"params": [NEO_scripthash, "getCandidates",
                    [], [
                        {"account": "0x48c40d4666f93408be1bef038b6722404d9a4c2a", "scopes": "CalledByEntry",
                         "allowedcontracts": [], "allowedgroups": []}]], "method": "invokefunction",
         "jsonrpc": "2.0", "id": 1}
    ))
    response_value = json.loads(response.text)['result']['stack'][0]['value']
    candidate_statistics = {
        base64.b64decode(candidate['value'][0]['value']).hex(): int(candidate['value'][1]['value'])
        for candidate in response_value
    }
    return candidate_statistics


@retry(RequestExceptions, tries=5, logger=None)
def fetch_agents():
    script = 'AAARwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSAAERwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSAAIRwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSAAMRwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSAAQRwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSAAURwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSAAYRwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSAAcRwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSAAgRwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSAAkRwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSAAoRwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSAAsRwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSAAwRwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSAA0RwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSAA4RwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSAA8RwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSABARwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSABERwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSABIRwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSABMRwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSABQRwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtS'
    response = session.post(url, data=json.dumps(
        {'params': [script, [{'account': BNEO_scripthash, 'scopes': 'CalledByEntry', 'allowedcontracts': [],
                              'allowedgroups': []}]], 'method': 'invokescript', 'jsonrpc': '2.0', 'id': 1}))
    agents = json.loads(response.text)['result']['stack']
    not_null_agent_indices = [i for i, agent in enumerate(agents) if 'value' in agent]
    little_endian_scripthashes = map(lambda x: base64.b64decode(x['value']).hex(), filter(lambda x: 'value' in x, agents))
    big_endian_scripthashes = map(lambda x: '0x'+''.join(reversed([x[i:i+2] for i in range(0, len(x), 2)])), little_endian_scripthashes)
    return list(big_endian_scripthashes), not_null_agent_indices


@retry(RequestExceptions, tries=5, logger=None)
def fetch_vote_target(agent_scripthash):
    response = session.post(url, data=json.dumps(
        {"params": [NEO_scripthash, "getAccountState",
                    [{"type": "Hash160", "value": agent_scripthash}], [
                        {"account": "0x48c40d4666f93408be1bef038b6722404d9a4c2a", "scopes": "CalledByEntry",
                         "allowedcontracts": [], "allowedgroups": []}]], "method": "invokefunction",
         "jsonrpc": "2.0", "id": 1}
    ))
    try:
        response_value = json.loads(response.text)['result']['stack'][0]['value']
    except KeyError:
        return None, None
    vote_NEO, vote_target = int(response_value[0]['value']), base64.b64decode(response_value[2]['value']).hex()
    return vote_NEO, vote_target


def calc_sorted_vote_stats_and_our_voting_vector() -> Tuple[List[Tuple[int, str]], List[int]]:
    agents, candidates = gevent.spawn(fetch_agents), gevent.spawn(fetch_candidates)
    agents.join()
    agents, not_null_agent_indices = agents.value
    current_vote_targets = [gevent.spawn(fetch_vote_target, agent_scripthash) for agent_scripthash in agents]
    gevent.joinall(current_vote_targets)
    current_vote_targets = [vote_target.value for vote_target in current_vote_targets if vote_target.value]
    candidates.join()
    candidates: Dict[str, int] = candidates.value
    sum_NEO = 0
    our_voting_vector = [0] * len(candidates)
    current_vote_targets_dict = {}
    for agent_neo, vote_target in current_vote_targets:
        sum_NEO += agent_neo
        # candidates[vote_target] -= agent_neo  # do not reduce our neo
        if vote_target in current_vote_targets_dict:
            current_vote_targets_dict[vote_target] += agent_neo
        else:
            current_vote_targets_dict[vote_target] = agent_neo
    vote_stats_to_sort = [(candidates[candidate], candidate) for candidate in candidates]
    sorted_vote_stats_index = [i[0] for i in sorted(enumerate(vote_stats_to_sort), key=lambda x: x[1][0], reverse=True)]
    sorted_vote_stats = [vote_stats_to_sort[i] for i in sorted_vote_stats_index]
    sorted_vote_stats_index_dict = {target: index for (current_vote, target), index in zip(sorted_vote_stats, sorted_vote_stats_index)}
    for target in current_vote_targets_dict:
        our_voting_vector[sorted_vote_stats_index_dict[target]] += current_vote_targets_dict[target]
    
    return sorted_vote_stats, our_voting_vector


def calc_gas_gain_per_second(sorted_vote_stats: List[Tuple[int, str]], our_voting_vector: List[int])\
        -> Tuple[Decimal, Decimal, Decimal]:
    """
    :param sorted_vote_stats: [(candidate_vote, candidate_public_key), ...]; our votes excluded;
        should be sorted by candidate_vote descending
    :param our_voting_vector: [our_vote for candidate of most votes from others,
        our_vote for candidante of 2nd most votes from others, ...]
    :return: (our GAS gain per second for holding NEO, our GAS gain per block for voting, sum of the previous two items)
    """
    reward_factor_list = [GAS_per_block_from_voting / 2 / 7] * 7 + [GAS_per_block_from_voting / 2 / 14] * 14 \
                    + [0] * (len(our_voting_vector) - (14+7))
    gas_from_holding_neo = Decimal(sum(our_voting_vector) * Decimal(GAS_per_block_from_holding_NEO)) / total_neo
    gas_from_holding_neo /= seconds_per_block
    gas_from_voting = Decimal(0.0)
    for (candidate_vote, candidate_public_key), our_vote, reward_factor in \
        zip(sorted_vote_stats, our_voting_vector, reward_factor_list):
        if candidate_vote:
            gas_from_voting += Decimal(our_vote * reward_factor) / Decimal(candidate_vote)
        # print(our_vote, candidate_vote)
    gas_from_voting /= seconds_per_block
    return gas_from_holding_neo, gas_from_voting, (gas_from_holding_neo + gas_from_voting)


if __name__ == '__main__':
    sorted_vote_stats, our_voting_vector = calc_sorted_vote_stats_and_our_voting_vector()
    gas_gain_per_second = calc_gas_gain_per_second(sorted_vote_stats, our_voting_vector)
    # print(sorted_vote_stats)
    # print(our_voting_vector)
    for value in gas_gain_per_second:
        print(value.real, end=' ')
    print()
    # print(gas_gain_per_second[2]*86400*365 * 60 / (290 * sum(our_voting_vector)))  # an approximate APR