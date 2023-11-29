# Delegactor

Still in beta/exporative develpment. [Read](./docs/ActorFramework.md) for a detailed understanding. 

reduced/moved from castle_proxy based invokation to code generation for poxy client 
- a bug was found, have to move out from castle proxy layer as its calls are blocking and that results in lower request rate
- poc was done to use code generation to autogen proxy layer using interfaces, it seems promising. 

This numbers are per cluster.

(old numbers )
![image](https://github.com/AICorpGlobal/Delegactor/assets/8478849/ae5ec8bf-a441-4d3d-9c9d-36966b46735b)

- new ones using code gen reaches RMQ headroom (10k-20k rps )
 ![image](https://github.com/AICorpGlobal/Delegactor/assets/8478849/f655920a-9a7f-40aa-acff-8df6fb370b34)


Won't be investing there(Performance) right now, instead will focus more on reliability and resilence


These numbers are without tuning, the Current RTT is 0.002

- to provide a baseline 
- untuned
- [Ryzen 5 3600x](https://www.techpowerup.com/cpu-specs/ryzen-5-3600.c2132)
- 32 gb 2400 ddr4
- [CPU stat](https://nanoreview.net/en/cpu/amd-ryzen-5-3600)

![image](https://github.com/jazeem-azeez/Delegactor/assets/8478849/c5b67bee-d7c2-4ab4-9640-5947084553f3) 



NOTE: Tests are pending and is not not fit for product yet as I'm still doing exploration,

I'm planning to put a white paper for the same. Interested personals can reach out to me for collaboration.

Further work include.

* Http Proxy
  - Http Interop like Dapr (in Go) target is a single proxy can easily handle 10k requests now. 
* Improve Healing
  - Have to Write TLA+ evaluation 
  - Improve Competing Agents/Consumer Pattern (the pattern that is being used here for cluster membership)
 
* Not using lock / reduce locks usage [Disrupter](https://github.com/disruptor-net/Disruptor-net)
  - remove locking, also have to move away from usage of default dictionary implementation as it is too slow, even a simple dictionary with a lock performs much better
  
* https://zeromq.org
  - move to us nearly broker less communication patter, so near linear scalbilty can be achived

* Seriliazation
  - Use GRPC/MessagePack

* Porting to other languages
   - Golang
   - Rust
   - C++
   - TypeScript/JavaScript
