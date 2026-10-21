"""Loopback-only FIFO latency fixture, with optional seeded per-direction jitter."""
import asyncio
import argparse
import random

async def relay(reader, writer, delay, jitter=0, seed=0):
    queue=asyncio.Queue(maxsize=256)
    rng=random.Random(seed)
    async def read():
        while True:
            data=await reader.read(65536)
            # FIFO write order intentionally retains TCP head-of-line blocking.
            offset=rng.uniform(-jitter,jitter) if jitter else 0
            await queue.put((asyncio.get_running_loop().time()+delay+offset,data))
            if not data: return
    async def write():
        while True:
            due,data=await queue.get()
            await asyncio.sleep(max(0,due-asyncio.get_running_loop().time()))
            if not data: return
            writer.write(data)
            await writer.drain()
    tasks=[asyncio.create_task(read()),asyncio.create_task(write())]
    try: await asyncio.gather(*tasks)
    finally:
        for task in tasks: task.cancel()
        writer.close()

async def main(args):
    ordinal=0
    async def connection(reader,writer):
        nonlocal ordinal
        ordinal+=1
        seed=args.seed+ordinal*2
        upstream=None
        try:
            incoming,upstream=await asyncio.open_connection('127.0.0.1',args.target)
            await asyncio.gather(relay(reader,upstream,args.delay_ms/1000,args.jitter_ms/1000,seed),
                                 relay(incoming,writer,args.delay_ms/1000,args.jitter_ms/1000,seed+1))
        except (ConnectionError,asyncio.CancelledError): pass
        finally:
            writer.close()
            if upstream: upstream.close()
    server=await asyncio.start_server(connection,'127.0.0.1',args.listen)
    print(f'DELAY_PROXY_READY one_way_ms={args.delay_ms} jitter_ms={args.jitter_ms} seed={args.seed}',flush=True)
    async with server: await asyncio.sleep(args.seconds)

if __name__=='__main__':
    p=argparse.ArgumentParser()
    p.add_argument('--listen',type=int,required=True);p.add_argument('--target',type=int,required=True)
    p.add_argument('--delay-ms',type=int,required=True);p.add_argument('--seconds',type=int,default=190)
    p.add_argument('--jitter-ms',type=int,default=0);p.add_argument('--seed',type=int,default=42)
    a=p.parse_args()
    if not 0<=a.delay_ms<=200: p.error('delay must be 0..200 ms')
    if not 0<=a.jitter_ms<=min(a.delay_ms,50): p.error('jitter must be 0..min(delay,50) ms')
    asyncio.run(main(a))
