//
//  SPDX-FileName: TypeExtensionTests.cs
//  SPDX-FileCopyrightText: Copyright (c) Jarl Gullberg
//  SPDX-License-Identifier: LGPL-3.0-or-later
//

using System.Collections.Generic;
using System.Linq;
using Remora.Rest.Core;
using Remora.Rest.Extensions;
using Xunit;

// ReSharper disable SA1600
#pragma warning disable 1591, SA1600

namespace Remora.Rest.Tests.Extensions;

/// <summary>
/// Tests the <see cref="Rest.Extensions.TypeExtensions"/> class.
/// </summary>
public class TypeExtensionTests
{
    public class IsOptional
    {
        [Fact]
        public void ReturnsFalseForNonOptionalType()
        {
            Assert.False(typeof(int).IsOptional());
        }

        [Fact]
        public void ReturnsFalseForNonOptionalGenericType()
        {
            Assert.False(typeof(List<int>).IsOptional());
        }

        [Fact]
        public void ReturnsTrueForOptionalType()
        {
            Assert.True(typeof(Optional<int>).IsOptional());
        }
    }

    public class IsNullable
    {
        [Fact]
        public void ReturnsFalseForNonNullableType()
        {
            Assert.False(typeof(int).IsNullable());
        }

        [Fact]
        public void ReturnsFalseForNonNullableGenericType()
        {
            Assert.False(typeof(List<int>).IsNullable());
        }

        [Fact]
        public void ReturnsTrueForNullableType()
        {
            Assert.True(typeof(int?).IsNullable());
        }
    }

    public class Unwrap
    {
        [Fact]
        public void UnwrapsOptional()
        {
            Assert.Equal(typeof(int), typeof(Optional<int>).Unwrap());
        }

        [Fact]
        public void UnwrapsOptionalNullable()
        {
            Assert.Equal(typeof(int), typeof(Optional<int?>).Unwrap());
        }

        [Fact]
        public void UnwrapsNullableOptional()
        {
            Assert.Equal(typeof(int), typeof(Optional<int>?).Unwrap());
        }

        [Fact]
        public void DoesNotChangeOtherGenericTypes()
        {
            Assert.Equal(typeof(List<int>), typeof(Optional<List<int>>).Unwrap());
        }

        [Fact]
        public void DoesNotChangeNonOptionalOrNonNullableType()
        {
            Assert.Equal(typeof(int), typeof(int).Unwrap());
        }
    }

    public class GetPublicProperties
    {
        // ReSharper disable UnassignedGetOnlyAutoProperty
        private interface IEmptyInterface
        {
        }

        private interface IInterfaceWithSingleProperty
        {
            int SingleProperty { get; }
        }

        private interface IInterfaceWithMultipleProperties
        {
            int FirstProperty { get; }

            int SecondProperty { get; }
        }

        private interface IInterfaceWithInheritedProperties
            :
                IInterfaceWithSingleProperty,
                IInterfaceWithMultipleProperties
        {
        }

        private class EmptyClass
        {
        }

        private class ClassWithSingleProperty
        {
            public int SingleProperty { get; }
        }

        private class ClassWithMultipleProperties
        {
            public int FirstProperty { get; }

            public int SecondProperty { get; }
        }

        private class ClassWithInheritedProperties
            :
                IInterfaceWithSingleProperty,
                IInterfaceWithMultipleProperties
        {
            public int SingleProperty { get; }

            public int FirstProperty { get; }

            public int SecondProperty { get; }
        }

        private class ClassWithInheritedAndExtraProperties
            :
                IInterfaceWithSingleProperty,
                IInterfaceWithMultipleProperties
        {
            public int SingleProperty { get; }

            public int FirstProperty { get; }

            public int SecondProperty { get; }

            public int ThirdProperty { get; }
        }

        // ReSharper restore UnassignedGetOnlyAutoProperty
        [Fact]
        public void ReturnsEmptyCollectionForEmptyInterface()
        {
            var properties = typeof(IEmptyInterface).GetPublicProperties();

            Assert.Empty(properties.ToArray());
        }

        [Fact]
        public void ReturnsSingleElementForInterfaceWithSingleProperty()
        {
            var properties = typeof(IInterfaceWithSingleProperty).GetPublicProperties();

            Assert.Single(properties.ToArray());
        }

        [Fact]
        public void ReturnsMultipleElementsForInterfaceWithMultipleProperties()
        {
            var properties = typeof(IInterfaceWithMultipleProperties).GetPublicProperties();

            Assert.True(properties.Count() > 1);
        }

        [Fact]
        public void ReturnsMultipleElementsForInterfaceWithInheritedProperties()
        {
            var properties = typeof(IInterfaceWithInheritedProperties).GetPublicProperties();

            Assert.True(properties.Count() > 1);
        }

        [Fact]
        public void ReturnsCorrectElementForInterfaceWithSingleProperty()
        {
            var properties = typeof(IInterfaceWithSingleProperty).GetPublicProperties();
            var singleProperty = typeof(IInterfaceWithSingleProperty).GetProperty
            (
                nameof(IInterfaceWithSingleProperty.SingleProperty)
            );

            Assert.Equal(singleProperty, properties.Single());
        }

        [Fact]
        public void ReturnsCorrectElementsForInterfaceWithMultipleProperties()
        {
            var properties = typeof(IInterfaceWithMultipleProperties).GetPublicProperties();
            var firstProperty = typeof(IInterfaceWithMultipleProperties).GetProperty
            (
                nameof(IInterfaceWithMultipleProperties.FirstProperty)
            );

            var secondProperty = typeof(IInterfaceWithMultipleProperties).GetProperty
            (
                nameof(IInterfaceWithMultipleProperties.SecondProperty)
            );

            Assert.Equal(new[] { firstProperty, secondProperty }, properties.ToArray());
        }

        [Fact]
        public void ReturnsCorrectElementsForInterfaceWithInheritedProperties()
        {
            var properties = typeof(IInterfaceWithInheritedProperties).GetPublicProperties();
            var singleProperty = typeof(IInterfaceWithSingleProperty).GetProperty
            (
                nameof(IInterfaceWithSingleProperty.SingleProperty)
            );

            var firstProperty = typeof(IInterfaceWithMultipleProperties).GetProperty
            (
                nameof(IInterfaceWithMultipleProperties.FirstProperty)
            );

            var secondProperty = typeof(IInterfaceWithMultipleProperties).GetProperty
            (
                nameof(IInterfaceWithMultipleProperties.SecondProperty)
            );

            Assert.Equal(new[] { singleProperty, firstProperty, secondProperty }, properties.ToArray());
        }

        [Fact]
        public void ReturnsEmptyCollectionForEmptyClass()
        {
            var properties = typeof(EmptyClass).GetPublicProperties();

            Assert.Empty(properties.ToArray());
        }

        [Fact]
        public void ReturnsSingleElementForClassWithSingleProperty()
        {
            var properties = typeof(ClassWithSingleProperty).GetPublicProperties();

            Assert.Single(properties.ToArray());
        }

        [Fact]
        public void ReturnsMultipleElementsForClassWithMultipleProperties()
        {
            var properties = typeof(ClassWithMultipleProperties).GetPublicProperties();

            Assert.True(properties.Count() > 1);
        }

        [Fact]
        public void ReturnsMultipleElementsForClassWithInheritedProperties()
        {
            var properties = typeof(ClassWithInheritedProperties).GetPublicProperties();

            Assert.True(properties.Count() > 1);
        }

        [Fact]
        public void ReturnsMultipleElementsForClassWithInheritedAndExtraProperties()
        {
            var properties = typeof(ClassWithInheritedAndExtraProperties).GetPublicProperties();

            Assert.True(properties.Count() > 1);
        }

        [Fact]
        public void ReturnsCorrectElementForClassWithSingleProperty()
        {
            var properties = typeof(ClassWithSingleProperty).GetPublicProperties();
            var singleProperty = typeof(ClassWithSingleProperty).GetProperty
            (
                nameof(ClassWithSingleProperty.SingleProperty)
            );

            Assert.Equal(singleProperty, properties.Single());
        }

        [Fact]
        public void ReturnsCorrectElementsForClassWithMultipleProperties()
        {
            var properties = typeof(ClassWithMultipleProperties).GetPublicProperties();
            var firstProperty = typeof(ClassWithMultipleProperties).GetProperty
            (
                nameof(ClassWithMultipleProperties.FirstProperty)
            );

            var secondProperty = typeof(ClassWithMultipleProperties).GetProperty
            (
                nameof(ClassWithMultipleProperties.SecondProperty)
            );

            Assert.Equal(new[] { firstProperty, secondProperty }, properties.ToArray());
        }

        [Fact]
        public void ReturnsCorrectElementsForClassWithInheritedProperties()
        {
            var properties = typeof(ClassWithInheritedProperties).GetPublicProperties();
            var singleProperty = typeof(ClassWithInheritedProperties).GetProperty
            (
                nameof(ClassWithInheritedProperties.SingleProperty)
            );

            var firstProperty = typeof(ClassWithInheritedProperties).GetProperty
            (
                nameof(ClassWithInheritedProperties.FirstProperty)
            );

            var secondProperty = typeof(ClassWithInheritedProperties).GetProperty
            (
                nameof(ClassWithInheritedProperties.SecondProperty)
            );

            Assert.Equal(new[] { singleProperty, firstProperty, secondProperty }, properties.ToArray());
        }

        [Fact]
        public void ReturnsCorrectElementsForClassWithInheritedAndExtraProperties()
        {
            var properties = typeof(ClassWithInheritedAndExtraProperties).GetPublicProperties();
            var singleProperty = typeof(ClassWithInheritedAndExtraProperties).GetProperty
            (
                nameof(ClassWithInheritedAndExtraProperties.SingleProperty)
            );

            var firstProperty = typeof(ClassWithInheritedAndExtraProperties).GetProperty
            (
                nameof(ClassWithInheritedAndExtraProperties.FirstProperty)
            );

            var secondProperty = typeof(ClassWithInheritedAndExtraProperties).GetProperty
            (
                nameof(ClassWithInheritedAndExtraProperties.SecondProperty)
            );

            var thirdProperty = typeof(ClassWithInheritedAndExtraProperties).GetProperty
            (
                nameof(ClassWithInheritedAndExtraProperties.ThirdProperty)
            );

            Assert.Equal
            (
                new[] { singleProperty, firstProperty, secondProperty, thirdProperty },
                properties.ToArray()
            );
        }
    }
}
