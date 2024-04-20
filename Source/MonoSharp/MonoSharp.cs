/***********************************************************************
** Written by Frost 8/15/2023 https://github.com/imerzan/MonoSharp
** Major Thanks to Reahly: https://github.com/reahly/mono-external-lib
************************************************************************/

using System.Text;

namespace eft_dma_radar {
    internal static class MonoSharp {
        public static bool InitializeFunctions() {
            if (Monolib.init_functions()) {
                Program.Log("Mono Init Funcs [OK]");
                return true;
            }
            Program.Log("Mono Init Funcs [FAIL]");
            return false;
        }

        public static ulong FindClass(string assembly_name, string class_name) {

            var monoClass = Monolib.find_class(assembly_name, class_name);
            if (monoClass == 0x0)
                throw new Exception("NULL " + nameof(monoClass));

            var deref = Memory.ReadValue<ulong>(monoClass + 0x0); // Deref at 0x0
            if (deref == 0x0)
                throw new Exception("NULL " + nameof(deref));
            return deref;
        }

        public static ulong FindMethodOfClass(string assembly_name, string class_name, string method_name) {
            var monoClass = Monolib.find_class(assembly_name, class_name);
            if (monoClass == 0x0)
                throw new Exception("NULL " + nameof(monoClass));
            var monoMethod = monoClass.find_method(method_name);
            if (monoMethod == 0x0)
                throw new Exception("NULL " + nameof(monoMethod));

            return monoMethod;
        }

        public static ulong GetStaticFieldDataOfClass(string assembly_name, string class_name) {
            var staticFieldData = Monolib.find_class(assembly_name, class_name).get_vtable(Monolib.get_root_domain()).get_static_field_data();
            if (staticFieldData == 0x0)
                throw new Exception("NULL " + nameof(staticFieldData));
            var deref = Memory.ReadValue<ulong>(staticFieldData + 0x0); // Deref at 0x0
            if (deref == 0x0)
                throw new Exception("NULL " + nameof(deref));
            return deref;
        }

        public static uint FindFieldOffsetInClass(string assembly_name, string class_name, string field_name) {
            return (uint)Monolib.find_class(assembly_name, class_name).find_field(field_name).offset();
        }

        private static class Monolib {
            public static Dictionary<ulong, ulong> functions = new();

            public static ushort utf8_to_utf16(string val) {
                var utf16Bytes = Encoding.Unicode.GetBytes(val);
                return BitConverter.ToUInt16(utf16Bytes, 0);
            }

            public static string read_widechar(ulong addr, int size) {
                try {
                    var buffer = Memory.ReadBuffer(addr, size);
                    return Encoding.UTF8.GetString(buffer).Split('\0')[0];
                } catch {
                    return string.Empty;
                }
            }

            public static mono_root_domain_t get_root_domain() {
                var mono_module = Memory.GetMonoModule();
                if (mono_module == 0x0)
                    return default;
                return new mono_root_domain_t(Memory.ReadValue<ulong>(mono_module + 0x499c78));
            }

            public static bool init_functions() {
                functions = new(); // Reset dictionary
                var rootDomain = get_root_domain();
                if (rootDomain == 0x0)
                    return false;
                var jitted_table = rootDomain.jitted_function_table();
                if (jitted_table == 0x0)
                    return false;
                int iCount = Memory.ReadValue<int>(jitted_table + 0x8);
                if (iCount > 10000) {
                    Program.Log($"{nameof(iCount)} out of bounds!");
                    return false;
                }
                for (int i = 0; i < iCount; i++) {
                    var entry = Memory.ReadValue<ulong>(jitted_table + 0x10 + (uint)i * 0x8);
                    if (entry == 0x0)
                        continue;
                    int jCount = Memory.ReadValue<int>(entry + 0x4);
                    if (jCount > 1000) {
                        Program.Log($"{nameof(jCount)} out of bounds!");
                        return false;
                    }
                    for (int j = 0; j < jCount; j++) {
                        var function = Memory.ReadValue<ulong>(entry + 0x18 + (uint)j * 0x8);
                        if (function == 0x0)
                            continue;

                        var mono_ptr = Memory.ReadValue<ulong>(function + 0x0);
                        var jitted_ptr = Memory.ReadValue<ulong>(function + 0x10);
                        functions[mono_ptr] = jitted_ptr;
                    }
                }
                return true;
            }

            public static mono_assembly_t domain_assembly_open(mono_root_domain_t domain, string name) {
                var domain_assemblies = domain.domain_assemblies();
                if (domain_assemblies == 0x0)
                    return default;

                ulong data = 0x0;
                while (true) {
                    data = domain_assemblies.data();
                    if (data == 0x0)
                        continue;

                    var data_name = read_widechar(Memory.ReadValue<ulong>(data + 0x10), 128);
                    if (data_name == name)
                        break;
                    domain_assemblies = new glist_t(domain_assemblies.next());
                    if (domain_assemblies == 0x0)
                        break;
                }

                return new mono_assembly_t(data);
            }

            public static mono_class_t find_class(string assembly_name, string class_name) {
                var root_domain = get_root_domain();
                if (root_domain == 0x0)
                    return default;
                var domain_assembly = domain_assembly_open(root_domain, assembly_name);
                if (domain_assembly == 0x0)
                    return default;
                var mono_image = domain_assembly.mono_image();
                if (mono_image == 0x0)
                    return default;
                var table_info = mono_image.get_table_info(2);
                if (table_info == 0x0)
                    return default;
                int rowCount = table_info.get_rows();
                if (rowCount > 25000) {
                    Program.Log($"{nameof(rowCount)} out of bounds!");
                    return default;
                }
                for (int i = 0; i < rowCount; i++) {
                    var ptr = new mono_class_t(new mono_hash_table_t(mono_image + 0x4C0).lookup((ulong)(0x02000000 | i + 1)));
                    if (ptr == 0x0)
                        continue;
                    var name = ptr.name();
                    var ns = ptr.namespace_name();
                    if (ns.Length != 0)
                        name = ns + "." + name;
                    if (name == class_name)
                        return ptr;
                }
                return default;
            }
        }

        public readonly struct glist_t {
            public static implicit operator ulong(glist_t x) => x.Base;
            private readonly ulong Base;

            public glist_t(ulong baseAddr) {
                Base = baseAddr;
            }

            public readonly ulong data() => Memory.ReadValue<ulong>(this + 0x0);
            public readonly ulong next() => Memory.ReadValue<ulong>(this + 0x8);
        }

        public readonly struct mono_root_domain_t {
            public static implicit operator ulong(mono_root_domain_t x) => x.Base;
            private readonly ulong Base;

            public mono_root_domain_t(ulong baseAddr) {
                Base = baseAddr;
            }

            public readonly glist_t domain_assemblies() => new glist_t(Memory.ReadValue<ulong>(this + 0xC8));
            public readonly int domain_id() => Memory.ReadValue<int>(this + 0xBC);
            public readonly ulong jitted_function_table() => Memory.ReadValue<ulong>(this + 0x148);
        }

        public readonly struct mono_table_info_t {
            public static implicit operator ulong(mono_table_info_t x) => x.Base;
            private readonly ulong Base;

            public mono_table_info_t(ulong baseAddr) {
                Base = baseAddr;
            }

            public readonly int get_rows() => Memory.ReadValue<int>(this + 0x8) & 0xFFFFFF;
        }

        public readonly struct mono_method_t {
            public static implicit operator ulong(mono_method_t x) => x.Base;
            private readonly ulong Base;

            public mono_method_t(ulong baseAddr) {
                Base = baseAddr;
            }

            public readonly string name() {
                var address = Memory.ReadValue<ulong>(this + 0x18);
                string name = Monolib.read_widechar(address, 128);

                if (name.Length > 0 && (byte)name[0] == 0xEE) {
                    var utf16Value = Monolib.utf8_to_utf16(name);
                    name = $"\\u{utf16Value:X4}";
                }

                return name;
            }
        }

        public readonly struct mono_class_field_t {
            public static implicit operator ulong(mono_class_field_t x) => x.Base;
            private readonly ulong Base;

            public mono_class_field_t(ulong baseAddr) {
                Base = baseAddr;
            }

            public readonly string name() {
                var address = Memory.ReadValue<ulong>(this + 0x8);
                string name = Monolib.read_widechar(address, 128);

                if (name.Length > 0 && (byte)name[0] == 0xEE) {
                    var utf16Value = Monolib.utf8_to_utf16(name);
                    name = $"\\u{utf16Value:X4}";
                }

                return name;
            }
            public readonly int offset() {
                return Memory.ReadValue<int>(this + 0x18);
            }
        }

        public readonly struct mono_class_runtime_info_t {
            public static implicit operator ulong(mono_class_runtime_info_t x) => x.Base;
            private readonly ulong Base;

            public mono_class_runtime_info_t(ulong baseAddr) {
                Base = baseAddr;
            }

            public readonly int max_domain() => Memory.ReadValue<int>(this + 0x0);
        }

        public readonly struct mono_vtable_t {
            public static implicit operator ulong(mono_vtable_t x) => x.Base;
            private readonly ulong Base;

            public mono_vtable_t(ulong baseAddr) {
                Base = baseAddr;
            }

            public readonly byte flags() => Memory.ReadValue<byte>(this + 0x30);

            public readonly ulong get_static_field_data() {
                if ((this.flags() & 4) != 0)
                    return Memory.ReadValue<ulong>(this + 0x40 + 8 * (uint)Memory.ReadValue<int>(Memory.ReadValue<ulong>(this + 0x0) + 0x5C));
                return 0x0;
            }
        }

        public readonly struct mono_class_t {
            public static implicit operator ulong(mono_class_t x) => x.Base;
            private readonly ulong Base;

            public mono_class_t(ulong baseAddr) {
                Base = baseAddr;
            }

            public readonly int num_fields() => Memory.ReadValue<int>(this + 0x100);
            public readonly mono_class_runtime_info_t runtime_info() => new mono_class_runtime_info_t(Memory.ReadValue<ulong>(this + 0xD0));

            public readonly string name() {
                var address = Memory.ReadValue<ulong>(this + 0x48);
                string name = Monolib.read_widechar(address, 128);

                if (name.Length > 0 && (byte)name[0] == 0xEE) {
                    var utf16Value = Monolib.utf8_to_utf16(name);
                    name = $"\\u{utf16Value:X4}";
                }

                return name;
            }

            public readonly string namespace_name() {
                var address = Memory.ReadValue<ulong>(this + 0x50);
                string name = Monolib.read_widechar(address, 128);

                if (name.Length > 0 && (byte)name[0] == 0xEE) {
                    var utf16Value = Monolib.utf8_to_utf16(name);
                    name = $"\\u{utf16Value:X4}";
                }

                return name;
            }

            public readonly int get_num_methods() {
                var v2 = (Memory.ReadValue<int>(this + 0x2A) & 7) - 1;
                switch (v2) {
                    case 0:
                    case 1:
                        return Memory.ReadValue<int>(this + 0xFC);

                    case 3:
                    case 5:
                        return 0;

                    case 4:
                        return Memory.ReadValue<int>(this + 0xF0);

                    default: break;
                }

                return 0;
            }

            public readonly mono_method_t get_method(int i) =>
                new mono_method_t(Memory.ReadValue<ulong>(Memory.ReadValue<ulong>(this + 0xA0) + 0x8 * (uint)i));


            public readonly mono_class_field_t get_field(int i) {
                var fieldsPtr = Memory.ReadValue<ulong>(this + 0x98);
                return new mono_class_field_t(fieldsPtr + (ulong)(0x20 * i));
            }


            public readonly mono_vtable_t get_vtable(mono_root_domain_t domain) {
                var runtime_info = new mono_class_runtime_info_t(this.runtime_info());
                if (runtime_info == 0x0)
                    return default;

                var domain_id = domain.domain_id();
                if (runtime_info.max_domain() < domain_id)
                    return default;

                return new mono_vtable_t(Memory.ReadValue<ulong>(runtime_info + 8 * (uint)domain_id + 8));
            }

            public readonly mono_method_t find_method(string method_name) {
                ulong monoPtr = 0x0;

                int methodCount = this.get_num_methods();
                if (methodCount > 10000) {
                    Program.Log($"{nameof(methodCount)} out of bounds!");
                    return default;
                }
                for (int i = 0; i < methodCount; i++) {
                    var method = this.get_method(i);

                    if (method == 0x0)
                        continue;

                    if (method.name() == method_name)
                        monoPtr = method;
                }

                return new mono_method_t(Monolib.functions[monoPtr]);
            }

            public readonly mono_class_field_t find_field(string field_name) {
                int fieldCount = this.num_fields();
                if (fieldCount > 10000) {
                    Program.Log($"{nameof(fieldCount)} out of bounds!");
                    return default;
                }
                for (int i = 0; i < fieldCount; i++) {
                    var field = this.get_field(i);
                    if (field == 0x0)
                        continue;
                    if (field.name() == field_name)
                        return new mono_class_field_t(field);
                }
                return default;
            }
        }

        public readonly struct mono_hash_table_t {
            public static implicit operator ulong(mono_hash_table_t x) => x.Base;
            private readonly ulong Base;

            public mono_hash_table_t(ulong baseAddr) {
                Base = baseAddr;
            }

            public readonly int size() => Memory.ReadValue<int>(this + 0x18);
            public readonly ulong data() => Memory.ReadValue<ulong>(this + 0x20);
            public readonly ulong next_value() => Memory.ReadValue<ulong>(this + 0x108);
            public readonly uint key_extract() => Memory.ReadValue<uint>(this + 0x58);

            public readonly ulong lookup(ulong key) {
                var v4 = new mono_hash_table_t(Memory.ReadValue<ulong>(data() + 0x8 * (ulong)((uint)key % this.size())));
                if (v4 == 0x0)
                    return default;

                while ((ulong)v4.key_extract() != key) {
                    v4 = new mono_hash_table_t(v4.next_value());
                    if (v4 == 0x0)
                        return default;
                }

                return v4;
            }
        }

        public readonly struct mono_image_t {
            public static implicit operator ulong(mono_image_t x) => x.Base;
            private readonly ulong Base;

            public mono_image_t(ulong baseAddr) {
                Base = baseAddr;
            }

            public readonly int flags() => Memory.ReadValue<int>(this + 0x1C);

            public readonly mono_table_info_t get_table_info(int table_id) {
                if (table_id > 55)
                    return default;
                return new mono_table_info_t(this + 0x10 * ((uint)table_id + 0xE));
            }

            public readonly mono_class_t get(int type_id) {
                if ((this.flags() & 0x20) != 0)
                    return default;
                if ((type_id & 0xFF000000) != 0x2000000)
                    return default;
                return new mono_class_t(new mono_hash_table_t(this + 0x4C0).lookup((ulong)type_id));
            }
        }

        public readonly struct mono_assembly_t {
            public static implicit operator ulong(mono_assembly_t x) => x.Base;
            private readonly ulong Base;

            public mono_assembly_t(ulong baseAddr) {
                Base = baseAddr;
            }

            public readonly mono_image_t mono_image()
                => new mono_image_t(Memory.ReadValue<ulong>(this + 0x60));
        }
    }
}